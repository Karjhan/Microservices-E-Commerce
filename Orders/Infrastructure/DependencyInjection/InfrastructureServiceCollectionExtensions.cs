using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Infrastructure.Audit;
using Infrastructure.Caching;
using Infrastructure.Configuration;
using Infrastructure.Messaging;
using Infrastructure.Messaging.Consumers;
using Infrastructure.Observability;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Shared.Security.Audit;
using StackExchange.Redis;

namespace Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.PropertyNameCaseInsensitive = true;
        });
        
        services.Configure<DatabaseOptions>(config.GetSection("Database"));
        services.Configure<RabbitMqOptions>(config.GetSection("RabbitMQ"));
        services.Configure<RedisOptions>(config.GetSection("Redis"));

        services.AddDbContext<OrdersDbContext>((sp, options) =>
        {
            var dbOptions = sp
                .GetRequiredService<IOptions<DatabaseOptions>>()
                .Value;

            options.UseNpgsql(dbOptions.ConnectionString);
        });

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IAuditWriter, EfAuditWriter>();
        services.AddScoped<IAuditReader, EfAuditReader>();

        var redisOptions = config.GetSection("Redis").Get<RedisOptions>();

        if (redisOptions is not null && !string.IsNullOrEmpty(redisOptions.ConnectionString))
        {
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<RedisOptions>>().Value;

                var configOptions = ConfigurationOptions.Parse(options.ConnectionString);

                configOptions.AbortOnConnectFail = false;
                configOptions.ConnectRetry = 10;

                return ConnectionMultiplexer.Connect(configOptions);
            });

            services.AddScoped<ICacheService, RedisCacheService>();
        }

        services.AddSingleton<IConnection>(sp =>
        {
            var options = sp
                .GetRequiredService<IOptions<RabbitMqOptions>>()
                .Value;

            var factory = new ConnectionFactory
            {
                HostName = options.Host,
                Port = options.Port,
                UserName = options.Username,
                Password = options.Password,
                VirtualHost = options.VirtualHost
            };

            return factory.CreateConnectionAsync().GetAwaiter().GetResult();
        });

        services.AddScoped<IEventPublisher, RabbitMqPublisher>();

        services.AddHostedService<PaymentCompletedConsumer>();
        services.AddHostedService<PaymentFailedConsumer>();

        services.AddObservability(config);

        return services;
    }
}
