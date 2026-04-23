using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;

namespace IntegrationTests.Fixtures;

public class TestEnvironment : IAsyncLifetime
{
    public INetwork Network { get; private set; } = null!;

    public PostgresFixture Postgres { get; private set; } = null!;
    public RabbitMqFixture RabbitMq { get; private set; } = null!;
    public AuthFixture Auth { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Network = new NetworkBuilder()
            .WithName($"test-network-{Guid.NewGuid()}")
            .Build();

        await Network.CreateAsync();

        Postgres = new PostgresFixture(Network);
        await Postgres.InitializeAsync();

        RabbitMq = new RabbitMqFixture(Network);
        await RabbitMq.InitializeAsync();

        Auth = new AuthFixture(Network);
        await Auth.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await Auth.DisposeAsync();
        await RabbitMq.DisposeAsync();
        await Postgres.DisposeAsync();
        await Network.DeleteAsync();
    }
}