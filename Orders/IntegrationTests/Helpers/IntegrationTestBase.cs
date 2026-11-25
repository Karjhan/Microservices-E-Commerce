using Shared.Security.TestSupport;

namespace IntegrationTests.Helpers;

public abstract class IntegrationTestBase : IClassFixture<OrdersApiFactory>
{
    protected readonly HttpClient Client;
    protected readonly OrdersApiFactory Factory;

    protected IntegrationTestBase(OrdersApiFactory factory)
    {
        Factory = factory;
        // Admin operations are gated (FR-001), so the default client is an administrator.
        Client = factory.CreateClient().AsAdmin();
    }
}
