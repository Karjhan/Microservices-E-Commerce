using FluentAssertions;
using IntegrationTests.Helpers;

namespace IntegrationTests.Endpoints;

public class UploadProductImageTests : IntegrationTestBase
{
    public UploadProductImageTests(CatalogApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Should_Upload_Image()
    {
        var productId = await ProductApi.CreateProductAsync(Client);

        var response = await ProductApi.UploadImageAsync(Client, productId, ProductApi.Png, isPrimary: true);

        response.EnsureSuccessStatusCode();
    }
}
