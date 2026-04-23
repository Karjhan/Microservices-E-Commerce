using DotNet.Testcontainers.Networks;
using Testcontainers.PostgreSql;

namespace IntegrationTests.Fixtures;

public class PostgresFixture : IAsyncLifetime
{
    private readonly INetwork _network;

    public PostgreSqlContainer Container { get; private set; } = null!;

    public string Hostname => "postgres";

    public PostgresFixture(INetwork network)
    {
        _network = network;
    }

    public async Task InitializeAsync()
    {
        Container = new PostgreSqlBuilder()
            .WithDatabase("ecommerce")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithNetwork(_network)
            .WithNetworkAliases(Hostname)
            .Build();

        await Container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await Container.DisposeAsync();
    }
}