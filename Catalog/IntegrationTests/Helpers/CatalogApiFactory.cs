using DotNet.Testcontainers.Builders;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using Minio;
using Minio.DataModel.Args;
using Testcontainers.Minio;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace IntegrationTests.Helpers;

public class CatalogApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres;
    private readonly RedisContainer _redis;
    private readonly RabbitMqContainer _rabbit;
    private readonly MinioContainer _minio;

    public string PostgresConnectionString => _postgres.GetConnectionString();
    public string RedisConnectionString => _redis.GetConnectionString();

    public CatalogApiFactory()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:17")
            .WithDatabase("catalog_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        _redis = new RedisBuilder()
            .WithImage("redis:7")
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
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _redis.StartAsync();
        await _rabbit.StartAsync();
        await _minio.StartAsync();
        //await WaitForMinio();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
        await _rabbit.DisposeAsync();
        await _minio.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((ctx, config) =>
        {
            var redisPort = _redis.GetMappedPublicPort(6379);
            var rabbitPort = _rabbit.GetMappedPublicPort(5672);
            var minioPort = _minio.GetMappedPublicPort(9000);

            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = _postgres.GetConnectionString(),

                ["Redis:ConnectionString"] = $"{_redis.Hostname}:{redisPort},abortConnect=false",

                ["RabbitMQ:Host"] = _rabbit.Hostname,
                ["RabbitMQ:Port"] = rabbitPort.ToString(),
                ["RabbitMQ:Username"] = "guest",
                ["RabbitMQ:Password"] = "guest",
                ["RabbitMQ:VirtualHost"] = "/",

                ["Minio:Endpoint"] = $"localhost:{minioPort}",
                ["Minio:AccessKey"] = "minioadmin",
                ["Minio:SecretKey"] = "minioadmin",
                ["Minio:Bucket"] = "products"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.PostConfigure<DbContextOptions<CatalogDbContext>>(options =>
            {
                // optional debugging hook
            });
        });
        
        builder.ConfigureServices(services =>
        {
            var sp = services.BuildServiceProvider();

            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

            db.Database.Migrate();
        });
    }
    
    private async Task WaitForMinio()
    {
        var endpoint = $"{_minio.Hostname}:{_minio.GetMappedPublicPort(9000)}";

        var client = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials("minioadmin", "minioadmin")
            .WithSSL(false)
            .Build();

        try
        {
            var exists = await client.BucketExistsAsync(
                new BucketExistsArgs()
                    .WithBucket("products"));

            if (!exists)
            {
                await client.MakeBucketAsync(
                    new MakeBucketArgs().WithBucket("products"));
            }
        }
        catch
        {
            throw new Exception("MinIO not ready after retries.");
        }
    }
}