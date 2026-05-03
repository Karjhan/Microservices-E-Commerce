using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;

namespace IntegrationTests.Fixtures;

public class RedisFixture : IAsyncLifetime
{
    private readonly INetwork _network;
    public IContainer Container { get; private set; } = null!;
    public string Hostname => "redis";

    public RedisFixture(INetwork network)
    {
        _network = network;
    }

    public async Task InitializeAsync()
    {
        Container = new ContainerBuilder()
            .WithImage("redis:7")
            .WithNetwork(_network)
            .WithNetworkAliases(Hostname)
            .WithPortBinding(6379, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(6379))
            .Build();

        await Container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await Container.DisposeAsync();
    }
}