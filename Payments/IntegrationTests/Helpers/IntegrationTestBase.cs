namespace IntegrationTests.Helpers;

public abstract class IntegrationTestBase : IClassFixture<PaymentsApiFactory>
{
    protected readonly HttpClient Client;
    protected readonly PaymentsApiFactory Factory;

    protected IntegrationTestBase(PaymentsApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }
}
