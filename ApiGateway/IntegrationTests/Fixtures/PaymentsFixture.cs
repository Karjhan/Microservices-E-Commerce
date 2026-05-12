using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using DotNet.Testcontainers.Networks;

namespace IntegrationTests.Fixtures;

public class PaymentsFixture : IAsyncLifetime
{
    private readonly INetwork _network;

    public IContainer Container { get; private set; } = null!;
    public string BaseUrl => $"http://localhost:{Container.GetMappedPublicPort(8080)}";

    public PaymentsFixture(INetwork network)
    {
        _network = network;
    }

    public async Task InitializeAsync()
    {
        Container = new ContainerBuilder()
            .WithImage("ecommerce-infra-payments:latest")
            .WithImagePullPolicy(PullPolicy.Never)
            .WithNetwork(_network)
            .WithNetworkAliases("payments")

            .WithPortBinding(8080, true)

            .WithEnvironment("Database__ConnectionString",
                "Host=postgres;Port=5432;Database=payments;Username=postgres;Password=postgres")

            .WithEnvironment("RabbitMQ__Host", "rabbitmq")
            .WithEnvironment("RabbitMQ__Port", "5672")
            .WithEnvironment("RabbitMQ__Username", "guest")
            .WithEnvironment("RabbitMQ__Password", "guest")
            .WithEnvironment("RabbitMQ__VirtualHost", "/")

            .WithEnvironment("Redis__ConnectionString", "redis:6379")

            .WithEnvironment("Stripe__SecretKey", "sk_test_fake_key_for_integration_tests")

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
