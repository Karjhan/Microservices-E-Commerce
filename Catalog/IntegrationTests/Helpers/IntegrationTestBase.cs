using Shared.Security.TestSupport;

namespace IntegrationTests.Helpers;

public abstract class IntegrationTestBase : IClassFixture<CatalogApiFactory>
{
    protected readonly HttpClient Client;
    protected readonly CatalogApiFactory Factory;

    protected IntegrationTestBase(CatalogApiFactory factory)
    {
        Factory = factory;

        // Admin operations are gated (FR-001), so the default client is an administrator.
        // Tests that need another caller create their own client with Factory.CreateClient().
        Client = factory.CreateClient().AsAdmin();
    }
}