using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using DotNet.Testcontainers.Networks;

namespace IntegrationTests.Fixtures;

public class AuthFixture : IAsyncLifetime
{
    private readonly INetwork _network;

    public IContainer Container { get; private set; } = null!;

    public string BaseUrl => $"http://localhost:{Container.GetMappedPublicPort(8080)}";

    public AuthFixture(INetwork network)
    {
        _network = network;
    }

    public async Task InitializeAsync()
    {
        Container = new ContainerBuilder()
            .WithImage("ecommerce-infra-auth:latest")
            .WithImagePullPolicy(PullPolicy.Never)
            .WithNetwork(_network)
            .WithNetworkAliases("auth")

            // expose externally
            .WithPortBinding(8080, true)

            // IMPORTANT: same hostnames as docker-compose
            .WithEnvironment("ConnectionStrings__Default",
                "Host=postgres;Port=5432;Database=ecommerce;Username=postgres;Password=postgres")
            
            .WithEnvironment("RabbitMq__Host", "rabbitmq")
            .WithEnvironment("RabbitMq__Port", "5672")
            .WithEnvironment("RabbitMq__Username", "guest")
            .WithEnvironment("RabbitMq__Password", "guest")

            .WithEnvironment("ASPNETCORE_URLS", "http://+:8080")
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development") 

            .WithWaitStrategy(Wait.ForUnixContainer())
            .Build();

        await Container.StartAsync();

        await WaitForHealthCheck();
    }

    private async Task WaitForHealthCheck()
    {
        using var http = new HttpClient();

        for (int i = 0; i < 20; i++)
        {
            try
            {
                var res = await http.GetAsync(BaseUrl + "/health");

                if (res.IsSuccessStatusCode)
                    return;
            }
            catch { }

            await Task.Delay(1000);
        }

        throw new Exception("Auth failed to start.");
    }

    public async Task DisposeAsync()
    {
        await Container.DisposeAsync();
    }
}