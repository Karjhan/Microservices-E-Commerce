namespace IntegrationTests.Common;

public class TestFixture : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await PostgresContainer.Instance.StartAsync();
        await RabbitMqTestContainer.Instance.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await PostgresContainer.Instance.StopAsync();
        await RabbitMqTestContainer.Instance.StopAsync();
    }
}