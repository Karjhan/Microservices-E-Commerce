using Shared.Security.TestSupport;

namespace IntegrationTests.Helpers;

public abstract class IntegrationTestBase : IClassFixture<PaymentsApiFactory>
{
    protected readonly HttpClient Client;
    protected readonly PaymentsApiFactory Factory;

    protected IntegrationTestBase(PaymentsApiFactory factory)
    {
        Factory = factory;
        // Admin operations are gated (FR-001), so the default client is an administrator.
        Client = factory.CreateClient().AsAdmin();
    }
}
