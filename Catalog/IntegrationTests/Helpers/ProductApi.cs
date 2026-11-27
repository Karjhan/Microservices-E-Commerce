using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace IntegrationTests.Helpers;

/// <summary>Request helpers shared by the product integration tests.</summary>
public static class ProductApi
{
    public static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
    public static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];
    public static readonly byte[] Executable = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00];

    public static object NewProductBody(string? name = null, decimal price = 1m) => new
    {
        Name = name ?? "Test Product " + Guid.NewGuid(),
        ShortDescription = "S",
        LongDescription = "L",
        Price = price,
        CategoryId = Guid.NewGuid(),
        Currency = "EUR",
        Settings = new { },
        Size = new { }
    };

    /// <summary>Creates a product (a Draft) and returns its id.</summary>
    public static async Task<Guid> CreateProductAsync(HttpClient client, string? name = null)
    {
        var res = await client.PostAsJsonAsync("/products", NewProductBody(name));
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<ProductResponse>();
        return body!.ProductId;
    }

    public static async Task<HttpResponseMessage> SetStatusAsync(HttpClient client, Guid productId, string status) =>
        await client.PostAsJsonAsync($"/products/{productId}/status", new { status });

    public static async Task<Guid> CreateActiveProductAsync(HttpClient client, string? name = null)
    {
        var id = await CreateProductAsync(client, name);
        (await SetStatusAsync(client, id, "Active")).EnsureSuccessStatusCode();
        return id;
    }

    public static Task<HttpResponseMessage> UploadImageAsync(
        HttpClient client, Guid productId, byte[] bytes, string fileName = "photo.png",
        string contentType = "image/png", bool? isPrimary = null)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(file, "file", fileName);
        if (isPrimary is not null) form.Add(new StringContent(isPrimary.Value ? "true" : "false"), "isPrimary");
        return client.PostAsync($"/products/{productId}/images", form);
    }

    public static Task<HttpResponseMessage> UploadModelAsync(
        HttpClient client, Guid productId, byte[] bytes, string fileName)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
        form.Add(file, "file", fileName);
        return client.PutAsync($"/products/{productId}/model", form);
    }

    public static async Task<JsonNode> ReadJsonAsync(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
}
