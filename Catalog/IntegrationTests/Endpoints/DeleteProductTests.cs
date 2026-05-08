using System.Net;
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

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Should_Delete_Incoming_Relations_When_Target_Product_Is_Deleted()
    {
        // Arrange
        var productAId = await CreateProduct();
        var productBId = await CreateProduct();

        var addRelation = await Client.PostAsJsonAsync($"/products/{productBId}/relations", new
        {
            RelatedProductId = productAId,
            Type = "similar"
        });
        addRelation.EnsureSuccessStatusCode();

        // Act
        var deleteResponse = await Client.DeleteAsync($"/products/{productAId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert
        var relationsResponse = await Client.GetAsync($"/products/{productBId}/relations");
        relationsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var relations = await relationsResponse.Content.ReadFromJsonAsync<List<RelationResponse>>();
        relations.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_Delete_Products_Own_Attributes_On_Delete()
    {
        // Arrange
        var productId = await CreateProduct();

        var addAttr = await Client.PostAsJsonAsync($"/products/{productId}/attributes", new
        {
            Key = "finish",
            Value = "matte"
        });
        addAttr.EnsureSuccessStatusCode();

        // Act
        var deleteResponse = await Client.DeleteAsync($"/products/{productId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert
        var getResponse = await Client.GetAsync($"/products/{productId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<Guid> CreateProduct()
    {
        var res = await Client.PostAsJsonAsync("/products", new
        {
            Name = "Test Product " + Guid.NewGuid(),
            ShortDescription = "S",
            LongDescription = "L",
            Price = 1m,
            CategoryId = Guid.NewGuid(),
            Currency = "EUR",
            Settings = new { },
            Size = new { }
        });

        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<ProductResponse>();
        return body!.ProductId;
    }

    private record RelationResponse(Guid RelationId, Guid RelatedProductId, string Type);
}