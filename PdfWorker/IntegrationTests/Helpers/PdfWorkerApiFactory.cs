using Domain.Entities;
using Domain.Enums;
using DotNet.Testcontainers.Builders;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.Minio;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace IntegrationTests.Helpers;

public class PdfWorkerApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private RabbitMqContainer? _rabbit;
    private MinioContainer? _minio;

    public async Task InitializeAsync()
    {
        if (!DockerGate.IsAvailable())
            return;

        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:17")
            .WithDatabase("pdfworker_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        _rabbit = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management")
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();

        _minio = new MinioBuilder()
            .WithImage("minio/minio:latest")
            .WithEnvironment("MINIO_ROOT_USER", "minioadmin")
            .WithEnvironment("MINIO_ROOT_PASSWORD", "minioadmin")
            .WithCommand("server /data --console-address :9001")
            .WithPortBinding(9000, true)
            .WithPortBinding(9001, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(req => req
                    .ForPort(9000)
                    .ForPath("/minio/health/ready")))
            .Build();

        await _postgres.StartAsync();
        await _rabbit.StartAsync();
        await _minio.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        if (_minio is not null) await _minio.DisposeAsync();
        if (_rabbit is not null) await _rabbit.DisposeAsync();
        if (_postgres is not null) await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            if (_postgres is null || _rabbit is null || _minio is null)
                return;

            var rabbitPort = _rabbit.GetMappedPublicPort(5672);
            var minioPort = _minio.GetMappedPublicPort(9000);

            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = _postgres.GetConnectionString(),
                ["RabbitMQ:Host"] = _rabbit.Hostname,
                ["RabbitMQ:Port"] = rabbitPort.ToString(),
                ["RabbitMQ:Username"] = "guest",
                ["RabbitMQ:Password"] = "guest",
                ["RabbitMQ:VirtualHost"] = "/",
                ["Minio:Endpoint"] = $"localhost:{minioPort}",
                ["Minio:AccessKey"] = "minioadmin",
                ["Minio:SecretKey"] = "minioadmin",
                ["Minio:Bucket"] = "documents",
                ["Orders:BaseUrl"] = "http://127.0.0.1:1",
                ["ServiceName"] = "pdf-worker-test",
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://127.0.0.1:4317",
                ["Seller:LegalName"] = "Test Seller GmbH",
                ["Seller:VatId"] = "DE999",
                ["Seller:Email"] = "billing@test.example",
                ["Seller:AddressLine1"] = "Teststr 1",
                ["Seller:City"] = "Munich",
                ["Seller:PostalCode"] = "80331",
                ["Seller:Country"] = "DE"
            });
        });

        builder.ConfigureServices(services =>
        {
            if (_postgres is null)
                return;

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PdfWorkerDbContext>();
            db.Database.Migrate();
        });
    }

    public async Task<Guid> SeedCompletedDocumentAsync(
        Guid orderId,
        Guid paymentId,
        Guid userId,
        DocumentType type = DocumentType.Invoice)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PdfWorkerDbContext>();

        var idempotencyKey = type switch
        {
            DocumentType.Invoice => $"invoice:{orderId}:{paymentId}",
            DocumentType.Receipt => $"receipt:{orderId}:{paymentId}",
            _ => $"credit:{Guid.NewGuid():N}"
        };

        var template = type switch
        {
            DocumentType.Invoice => "invoice-v1",
            DocumentType.Receipt => "receipt-v1",
            _ => "credit-note-v1"
        };

        var doc = Document.CreatePending(
            type,
            idempotencyKey,
            template,
            orderId,
            paymentId,
            userId,
            "payment.completed",
            "corr-test",
            "{}");

        doc.MarkCompleted("documents", $"{type}/test.pdf", "application/pdf", 100);

        await db.Documents.AddAsync(doc);
        await db.SaveChangesAsync();

        return doc.Id;
    }
}
