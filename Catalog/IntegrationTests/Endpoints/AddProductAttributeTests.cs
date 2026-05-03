using System.Net.Http.Json;
using FluentAssertions;
using IntegrationTests.Helpers;

namespace IntegrationTests.Endpoints;

public class AddProductAttributeTests : IntegrationTestBase
{
    public AddProductAttributeTests(CatalogApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Should_Add_Attribute()
    {
        var productId = await CreateProduct();

        var request = new
        {
            Key = "Color",
            Value = "Red"
        };

        var response = await Client.PostAsJsonAsync($"/products/{productId}/attributes", request);

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<AttributeResponse>();

        (body!.AttributeId).Should().NotBeEmpty();
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

public class AttributeResponse
{
    public Guid AttributeId { get; set; }
}