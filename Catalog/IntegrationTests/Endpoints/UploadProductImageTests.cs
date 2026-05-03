using System.Net.Http.Headers;
using System.Net.Http.Json;
using IntegrationTests.Helpers;

namespace IntegrationTests.Endpoints;

public class UploadProductImageTests : IntegrationTestBase
{
    public UploadProductImageTests(CatalogApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Should_Upload_Image()
    {
        var productId = await CreateProduct();

        var filePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(filePath, "fake image content");

        using var form = new MultipartFormDataContent();

        var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(filePath));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");

        form.Add(fileContent, "file", "test.png");
        form.Add(new StringContent("true"), "isPrimary");

        var response = await Client.PostAsync($"/products/{productId}/images", form);

        response.EnsureSuccessStatusCode();
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