namespace IntegrationTests.Helpers;

public abstract class IntegrationTestBase : IClassFixture<OrdersApiFactory>
{
    protected readonly HttpClient Client;
    protected readonly OrdersApiFactory Factory;

    protected IntegrationTestBase(OrdersApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }
}
