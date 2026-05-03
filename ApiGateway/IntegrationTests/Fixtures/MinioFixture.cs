using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;

namespace IntegrationTests.Fixtures;

public class MinioFixture : IAsyncLifetime
{
    private readonly INetwork _network;
    public IContainer Container { get; private set; } = null!;
    public string Hostname => "minio";

    public MinioFixture(INetwork network)
    {
        _network = network;
    }

    public async Task InitializeAsync()
    {
        Container = new ContainerBuilder()
            .WithImage("minio/minio:latest")
            .WithNetwork(_network)
            .WithNetworkAliases(Hostname)
            .WithPortBinding(9000, true)
            .WithCommand("server", "/data", "--console-address", ":9001")
            .WithEnvironment("MINIO_ROOT_USER", "minioadmin")
            .WithEnvironment("MINIO_ROOT_PASSWORD", "minioadmin")
            .WithWaitStrategy(
                Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(9000)
            )
            .Build();

        await Container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await Container.DisposeAsync();
    }
}