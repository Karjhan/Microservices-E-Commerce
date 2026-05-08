using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;

namespace IntegrationTests.Fixtures;

public class OllamaFixture : IAsyncLifetime
{
    private readonly INetwork _network;

    public IContainer Container { get; private set; } = null!;

    public OllamaFixture(INetwork network)
    {
        _network = network;
    }

    public async Task InitializeAsync()
    {
        Container = new ContainerBuilder()
            .WithImage("ollama/ollama:latest")
            .WithNetwork(_network)
            .WithNetworkAliases("ollama")
            .WithPortBinding(11434, true)
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(r => r.ForPort(11434).ForPath("/"))
            )
            .Build();

        await Container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await Container.DisposeAsync();
    }
}
