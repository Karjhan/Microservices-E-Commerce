using System.Net.Http.Json;
using FluentAssertions;
using IntegrationTests.Helpers;

namespace IntegrationTests.Endpoints;

public class DeleteProductTests : IntegrationTestBase
{
    public DeleteProductTests(CatalogApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Should_Delete_Product()
    {
        var productId = await CreateProduct();

        var response = await Client.DeleteAsync($"/products/{productId}");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);
    }

    private async Task<Guid> CreateProduct()
    {
        var res = await Client.PostAsJsonAsync("/products", new
        {
            Name = "Test",
            ShortDescription = "S",
            LongDescription = "L",
            Price = 1m,
            CategoryId = Guid.NewGuid(),
            Currency = "EUR",
            Settings = new { },
            Size = new { }
        });

        var body = await res.Content.ReadFromJsonAsync<ProductResponse>();
        return body!.ProductId;
    }
}