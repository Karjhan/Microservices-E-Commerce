using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;

namespace IntegrationTests.Fixtures;

public class Neo4jFixture : IAsyncLifetime
{
    private readonly INetwork _network;

    public IContainer Container { get; private set; } = null!;

    public Neo4jFixture(INetwork network)
    {
        _network = network;
    }

    public async Task InitializeAsync()
    {
        Container = new ContainerBuilder()
            .WithImage("neo4j:latest")
            .WithNetwork(_network)
            .WithNetworkAliases("neo4j")
            .WithPortBinding(7474, true)
            .WithPortBinding(7687, true)
            .WithEnvironment("NEO4J_AUTH", "neo4j/neo4jpassword")
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(r => r.ForPort(7474).ForPath("/"))
            )
            .Build();

        await Container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await Container.DisposeAsync();
    }
}