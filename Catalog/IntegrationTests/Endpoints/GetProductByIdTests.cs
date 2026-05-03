using System.Net.Http.Json;
using FluentAssertions;
using IntegrationTests.Helpers;

namespace IntegrationTests.Endpoints;

public class GetProductByIdTests : IntegrationTestBase
{
    public GetProductByIdTests(CatalogApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Should_Return_Product()
    {
        var productId = await CreateProduct();

        var response = await Client.GetAsync($"/products/{productId}");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        json.Should().Contain(productId.ToString());
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