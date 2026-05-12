using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Payments;
using Application.Abstractions.Persistence;
using Infrastructure.BackgroundJobs;
using Infrastructure.Caching;
using Infrastructure.Configuration;
using Infrastructure.Messaging;
using Infrastructure.Observability;
using Infrastructure.Payments;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using StackExchange.Redis;
using Stripe;

namespace Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.Configure<DatabaseOptions>(config.GetSection("Database"));
        services.Configure<RabbitMqOptions>(config.GetSection("RabbitMQ"));
        services.Configure<RedisOptions>(config.GetSection("Redis"));
        services.Configure<StripeOptions>(config.GetSection("Stripe"));

        services.AddDbContext<PaymentsDbContext>((sp, options) =>
        {
            var dbOptions = sp
                .GetRequiredService<IOptions<DatabaseOptions>>()
                .Value;

            options.UseNpgsql(dbOptions.ConnectionString);
        });

        services.AddScoped<IPaymentRepository, PaymentRepository>();

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
        services.AddSingleton<IStripeClient>(sp =>
        {
            var stripeOptions = sp.GetRequiredService<IOptions<StripeOptions>>().Value;
            return new StripeClient(stripeOptions.SecretKey);
        });

        services.AddScoped<IPaymentProvider, StripePaymentProvider>();

        services.AddHostedService<ExpiredPaymentJob>();

        services.AddObservability(config);

        return services;
    }
}
