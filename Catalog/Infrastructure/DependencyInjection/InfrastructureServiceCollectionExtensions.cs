using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using Infrastructure.Caching;
using Infrastructure.Configuration;
using Infrastructure.Messaging;
using Infrastructure.Observability;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;
using RabbitMQ.Client;
using StackExchange.Redis;

namespace Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.Configure<DatabaseOptions>(config.GetSection("Database"));
        services.Configure<RabbitMqOptions>(config.GetSection("RabbitMQ"));
        services.Configure<MinioOptions>(config.GetSection("Minio"));
        services.Configure<RedisOptions>(config.GetSection("Redis"));

        services.AddDbContext<CatalogDbContext>((sp, options) =>
        {
            var dbOptions = sp
                .GetRequiredService<IOptions<DatabaseOptions>>()
                .Value;

            options.UseNpgsql(dbOptions.ConnectionString);
        });

        services.AddScoped<IProductRepository, ProductRepository>();

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

        services.AddSingleton<IMinioClient>(sp =>
        {
            var options = sp
                .GetRequiredService<IOptions<MinioOptions>>()
                .Value;

            return new MinioClient()
                .WithEndpoint(options.Endpoint.Replace("http://", "").Replace("https://", ""))
                .WithCredentials(options.AccessKey, options.SecretKey)
                .WithSSL(false)
                .Build();
        });

        services.AddScoped<IFileStorageService, MinioFileStorageService>();

        services.AddObservability(config);

        return services;
    }
}