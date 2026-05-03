namespace IntegrationTests.Helpers;

public abstract class IntegrationTestBase : IClassFixture<CatalogApiFactory>
{
    protected readonly HttpClient Client;
    protected readonly CatalogApiFactory Factory;

    protected IntegrationTestBase(CatalogApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }
}