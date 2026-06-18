using Application.Abstractions.Enrichment;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Abstractions.Rendering;
using Application.Abstractions.Storage;
using Application.Mapping;
using Infrastructure.Configuration;
using Infrastructure.Enrichment;
using Infrastructure.Messaging;
using Infrastructure.Messaging.Consumers;
using Infrastructure.Observability;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Infrastructure.Rendering;
using Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Minio;
using RabbitMQ.Client;

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
        services.Configure<Application.Mapping.SellerOptions>(config.GetSection("Seller"));
        services.Configure<EnrichmentOptions>(config.GetSection("Enrichment"));
        services.Configure<OrdersOptions>(config.GetSection("Orders"));

        services.AddDbContext<PdfWorkerDbContext>((sp, options) =>
        {
            var dbOptions = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseNpgsql(dbOptions.ConnectionString);
        });

        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IPdfRenderer, QuestPdfRenderer>();
        services.AddScoped<IObjectStorageService, MinioObjectStorageService>();
        services.AddScoped<IEventPublisher, RabbitMqPublisher>();

        services.AddSingleton<IConnection>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
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

        services.AddSingleton<IMinioClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MinioOptions>>().Value;
            return new MinioClient()
                .WithEndpoint(options.Endpoint.Replace("http://", "").Replace("https://", ""))
                .WithCredentials(options.AccessKey, options.SecretKey)
                .WithSSL(options.Endpoint.StartsWith("https", StringComparison.OrdinalIgnoreCase))
                .Build();
        });

        services.AddHttpClient<IOrderEnrichmentClient, OrdersHttpClient>((sp, client) =>
        {
            var ordersOptions = sp.GetRequiredService<IOptions<OrdersOptions>>().Value;
            var enrichmentOptions = sp.GetRequiredService<IOptions<EnrichmentOptions>>().Value;
            client.BaseAddress = new Uri(ordersOptions.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(enrichmentOptions.OrdersTimeoutSeconds);
        })
        .AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 2;
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddHostedService<PaymentCompletedConsumer>();
        services.AddHostedService<PaymentRefundedConsumer>();
        services.AddHostedService<RabbitMqTopologyInitializer>();

        services.AddObservability(config);

        return services;
    }
}

public class RabbitMqTopologyInitializer : IHostedService
{
    private readonly IConnection _connection;

    public RabbitMqTopologyInitializer(IConnection connection)
    {
        _connection = connection;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: RabbitMqPublisher.ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            cancellationToken: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}