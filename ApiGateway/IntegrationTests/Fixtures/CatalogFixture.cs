using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using DotNet.Testcontainers.Networks;

namespace IntegrationTests.Fixtures;

public class CatalogFixture : IAsyncLifetime
{
    private readonly INetwork _network;

    public IContainer Container { get; private set; } = null!;
    public string BaseUrl => $"http://localhost:{Container.GetMappedPublicPort(8080)}";

    public CatalogFixture(INetwork network)
    {
        _network = network;
    }

    public async Task InitializeAsync()
    {
        Container = new ContainerBuilder()
            .WithImage("ecommerce-infra-catalog:latest")
            .WithImagePullPolicy(PullPolicy.Never)
            .WithNetwork(_network)
            .WithNetworkAliases("catalog")

            .WithPortBinding(8080, true)

            // DB
            .WithEnvironment("Database__ConnectionString",
                "Host=postgres;Port=5432;Database=ecommerce;Username=postgres;Password=postgres")

            // Rabbit
            .WithEnvironment("RabbitMQ__Host", "rabbitmq")
            .WithEnvironment("RabbitMQ__Port", "5672")
            .WithEnvironment("RabbitMQ__Username", "guest")
            .WithEnvironment("RabbitMQ__Password", "guest")
            .WithEnvironment("RabbitMQ__VirtualHost", "/")

            // Redis
            .WithEnvironment("Redis__ConnectionString", "redis:6379")

            // MinIO
            .WithEnvironment("Minio__Endpoint", "minio:9000")
            .WithEnvironment("Minio__AccessKey", "minioadmin")
            .WithEnvironment("Minio__SecretKey", "minioadmin")
            .WithEnvironment("Minio__Bucket", "products")

            .WithEnvironment("ASPNETCORE_URLS", "http://+:8080")
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")

            .WithWaitStrategy(
                Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8080).ForPath("/health"))
            )
            .Build();

        await Container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await Container.DisposeAsync();
    }
}