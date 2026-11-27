using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace IntegrationTests.Helpers;

/// <summary>Request helpers for brands, categories and filament types.</summary>
public static class ReferenceDataApi
{
    public static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..Math.Min(70, prefix.Length + 33)];

    private static async Task<Guid> CreateAsync(HttpClient client, string path, object body)
    {
        var response = await client.PostAsJsonAsync(path, body);
        response.EnsureSuccessStatusCode();

        var json = await ProductApi.ReadJsonAsync(response);
        return json["id"]!.GetValue<Guid>();
    }

    public static Task<Guid> CreateBrandAsync(HttpClient client, string? name = null) =>
        CreateAsync(client, "/brands", new { name = name ?? UniqueName("Brand") });

    public static Task<Guid> CreateCategoryAsync(HttpClient client, string? name = null) =>
        CreateAsync(client, "/categories", new { name = name ?? UniqueName("Category") });

    public static Task<Guid> CreateFilamentTypeAsync(
        HttpClient client, string? name = null, string? code = null, decimal? diameterMm = 1.75m) =>
        CreateAsync(client, "/filament-types", new { name = name ?? UniqueName("Filament"), code, diameterMm });

    public static Task<HttpResponseMessage> SetActiveAsync(HttpClient client, string resource, Guid id, bool active) =>
        client.PostAsync($"/{resource}/{id}/{(active ? "activate" : "deactivate")}", null);

    /// <summary>A product body with the given classification; leave arguments out for the defaults.</summary>
    public static object ProductBody(
        string? name = null,
        Guid? categoryId = null,
        Guid? brandId = null,
        Guid[]? filamentTypeIds = null) => new
    {
        Name = name ?? "Test Product " + Guid.NewGuid(),
        ShortDescription = "S",
        LongDescription = "L",
        Price = 1m,
        CategoryId = categoryId ?? CatalogApiFactory.DefaultCategoryId,
        BrandId = brandId,
        FilamentTypeIds = filamentTypeIds,
        Currency = "EUR",
        Settings = new { },
        Size = new { }
    };

    public static async Task<Guid> CreateProductAsync(
        HttpClient client, Guid? categoryId = null, Guid? brandId = null, Guid[]? filamentTypeIds = null,
        string? name = null)
    {
        var response = await client.PostAsJsonAsync(
            "/products", ProductBody(name, categoryId, brandId, filamentTypeIds));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ProductResponse>())!.ProductId;
    }

    public static Task<HttpResponseMessage> UploadLogoAsync(
        HttpClient client, Guid brandId, byte[] bytes, string fileName = "logo.png", string contentType = "image/png")
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(file, "file", fileName);

        return client.PutAsync($"/brands/{brandId}/logo", form);
    }
}
