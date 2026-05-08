using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;

namespace IntegrationTests.Fixtures;

public class QdrantFixture : IAsyncLifetime
{
    private readonly INetwork _network;

    public IContainer Container { get; private set; } = null!;

    public QdrantFixture(INetwork network)
    {
        _network = network;
    }

    public async Task InitializeAsync()
    {
        Container = new ContainerBuilder()
            .WithImage("qdrant/qdrant:latest")
            .WithNetwork(_network)
            .WithNetworkAliases("qdrant")
            .WithPortBinding(6333, true)
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(r => r.ForPort(6333).ForPath("/healthz"))
            )
            .Build();

        await Container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await Container.DisposeAsync();
    }
}