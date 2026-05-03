using System.Net.Http.Json;
using FluentAssertions;
using IntegrationTests.Helpers;

namespace IntegrationTests.Endpoints;

public class CreateProductTests : IntegrationTestBase
{
    public CreateProductTests(CatalogApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Should_Create_Product()
    {
        var request = new
        {
            Name = "Test Product",
            ShortDescription = "Short",
            LongDescription = "Long",
            Price = 10.5m,
            CategoryId = Guid.NewGuid(),
            Currency = "EUR",
            Settings = new { },
            Size = new { },
            Tags = new[] { "tag1" },
            SupportedMaterials = new[] { 0 },
            CompatiblePrinters = new[] { 0 }
        };

        var response = await Client.PostAsJsonAsync("/products", request);

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ProductResponse>();

        body!.ProductId.Should().NotBeEmpty();
    }
}

