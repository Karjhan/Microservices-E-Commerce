using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace IntegrationTests.Helpers;

public class OrdersApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres;
    private readonly RedisContainer _redis;
    private readonly RabbitMqContainer _rabbit;

    public OrdersApiFactory()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:17")
            .WithDatabase("orders_test")
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
    }

    public string RabbitHost => _rabbit.Hostname;
    public int RabbitPort => _rabbit.GetMappedPublicPort(5672);

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _redis.StartAsync();
        await _rabbit.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
        await _rabbit.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((ctx, config) =>
        {
            var redisPort = _redis.GetMappedPublicPort(6379);
            var rabbitPort = _rabbit.GetMappedPublicPort(5672);

            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = _postgres.GetConnectionString(),

                ["Redis:ConnectionString"] = $"{_redis.Hostname}:{redisPort},abortConnect=false",

                ["RabbitMQ:Host"] = _rabbit.Hostname,
                ["RabbitMQ:Port"] = rabbitPort.ToString(),
                ["RabbitMQ:Username"] = "guest",
                ["RabbitMQ:Password"] = "guest",
                ["RabbitMQ:VirtualHost"] = "/"
            });
        });

        builder.ConfigureServices(services =>
        {
            var sp = services.BuildServiceProvider();

            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

            db.Database.EnsureCreated();
        });
    }
}
