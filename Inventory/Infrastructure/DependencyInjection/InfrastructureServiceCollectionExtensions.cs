using Application.Abstractions.Caching;
using Application.Abstractions.Inventory;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Infrastructure.BackgroundJobs;
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
        services.Configure<InventoryOptions>(config.GetSection("Inventory"));

        services.AddDbContext<InventoryDbContext>((sp, options) =>
        {
            var dbOptions = sp
                .GetRequiredService<IOptions<DatabaseOptions>>()
                .Value;

            options.UseNpgsql(dbOptions.ConnectionString);
        });

        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddSingleton<IReservationPolicy, ReservationPolicy>();

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

        services.AddHostedService<OrderCreatedConsumer>();
        services.AddHostedService<OrderCancelledConsumer>();
        services.AddHostedService<PaymentCompletedConsumer>();
        services.AddHostedService<PaymentFailedConsumer>();
        services.AddHostedService<PaymentCancelledConsumer>();

        services.AddHostedService<ReservationExpiryJob>();

        services.AddObservability(config);

        return services;
    }
}
