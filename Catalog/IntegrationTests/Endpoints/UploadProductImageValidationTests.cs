using System.Net;using System.Net.Http.Json;
using FluentAssertions;
using IntegrationTests.Helpers;

namespace IntegrationTests.Endpoints;

/// <summary>US2: image uploads are validated by content, not by name or declared type (FR-009, FR-010).</summary>
public class UploadProductImageValidationTests : IntegrationTestBase
{
    public UploadProductImageValidationTests(CatalogApiFactory factory) : base(factory) { }

    [Fact]
    public async Task A_Renamed_Executable_Is_Refused_With_415()
    {
        var productId = await ProductApi.CreateProductAsync(Client);

        var response = await ProductApi.UploadImageAsync(Client, productId, ProductApi.Executable, "malware.png", "image/png");

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        (await ProductApi.ReadJsonAsync(response))["code"]!.GetValue<string>().Should().Be("unsupported_media_type");
        (await Factory.ListKeysAsync(CatalogApiFactory.ImageBucket, $"products/{productId}/")).Should().BeEmpty();
    }

    [Fact]
    public async Task An_Image_Over_Five_Megabytes_Is_Refused_With_413()
    {
        var productId = await ProductApi.CreateProductAsync(Client);
        var bytes = new byte[5 * 1024 * 1024 + 1];
        ProductApi.Png.CopyTo(bytes, 0);

        var response = await ProductApi.UploadImageAsync(Client, productId, bytes);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        (await ProductApi.ReadJsonAsync(response))["code"]!.GetValue<string>().Should().Be("payload_too_large");
    }

    [Fact]
    public async Task A_Real_Image_With_A_Misleading_Declared_Type_Is_Accepted()
    {
        var productId = await ProductApi.CreateProductAsync(Client);

        var response = await ProductApi.UploadImageAsync(Client, productId, ProductApi.Jpeg, "photo.jpg", "application/octet-stream");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ProductApi.ReadJsonAsync(response))["contentType"]!.GetValue<string>().Should().Be("image/jpeg");
    }

    [Fact]
    public async Task The_First_Image_Becomes_Primary_And_Later_Ones_Are_Appended()
    {
        var productId = await ProductApi.CreateProductAsync(Client);

        var first = await ProductApi.ReadJsonAsync(await ProductApi.UploadImageAsync(Client, productId, ProductApi.Png));
        var second = await ProductApi.ReadJsonAsync(await ProductApi.UploadImageAsync(Client, productId, ProductApi.Jpeg, "b.jpg", "image/jpeg"));

        first["isPrimary"]!.GetValue<bool>().Should().BeTrue();
        first["sortOrder"]!.GetValue<int>().Should().Be(0);
        second["isPrimary"]!.GetValue<bool>().Should().BeFalse();
        second["sortOrder"]!.GetValue<int>().Should().Be(1);
    }

    [Fact]
    public async Task Uploading_A_New_Primary_Image_Demotes_The_Previous_One()
    {
        var productId = await ProductApi.CreateProductAsync(Client);
        await ProductApi.UploadImageAsync(Client, productId, ProductApi.Png);

        var response = await ProductApi.UploadImageAsync(Client, productId, ProductApi.Jpeg, "b.jpg", "image/jpeg", isPrimary: true);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var product = await ProductApi.ReadJsonAsync(await Client.GetAsync($"/products/{productId}"));
        var images = product["images"]!.AsArray();
        images.Count(i => i!["isPrimary"]!.GetValue<bool>()).Should().Be(1);
    }

    [Fact]
    public async Task Deleting_The_Primary_Promotes_Another_And_Removes_The_File()
    {
        var productId = await ProductApi.CreateProductAsync(Client);
        var first = await ProductApi.ReadJsonAsync(await ProductApi.UploadImageAsync(Client, productId, ProductApi.Png));
        await ProductApi.UploadImageAsync(Client, productId, ProductApi.Jpeg, "b.jpg", "image/jpeg");
        var firstId = first["imageId"]!.GetValue<Guid>();

        var delete = await Client.DeleteAsync($"/products/{productId}/images/{firstId}");

        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Factory.ListKeysAsync(CatalogApiFactory.ImageBucket, $"products/{productId}/images/"))
            .Should().HaveCount(1).And.NotContain(k => k.EndsWith(firstId.ToString()));

        var product = await ProductApi.ReadJsonAsync(await Client.GetAsync($"/products/{productId}"));
        product["images"]!.AsArray().Single()!["isPrimary"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public async Task Images_Can_Be_Reordered_And_The_Primary_Changed()
    {
        var productId = await ProductApi.CreateProductAsync(Client);
        var a = (await ProductApi.ReadJsonAsync(await ProductApi.UploadImageAsync(Client, productId, ProductApi.Png)))["imageId"]!.GetValue<Guid>();
        var b = (await ProductApi.ReadJsonAsync(await ProductApi.UploadImageAsync(Client, productId, ProductApi.Jpeg, "b.jpg", "image/jpeg")))["imageId"]!.GetValue<Guid>();

        var reorder = await Client.PutAsJsonAsync($"/products/{productId}/images/order", new { imageIds = new[] { b, a } });
        var primary = await Client.PostAsync($"/products/{productId}/images/{b}/primary", null);

        reorder.StatusCode.Should().Be(HttpStatusCode.OK);
        primary.StatusCode.Should().Be(HttpStatusCode.OK);

        var images = (await ProductApi.ReadJsonAsync(await Client.GetAsync($"/products/{productId}")))["images"]!.AsArray();
        images.Select(i => i!["id"]?.GetValue<Guid>() ?? i["imageId"]!.GetValue<Guid>()).Should().Equal(b, a);
        images[0]!["isPrimary"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public async Task Reordering_With_A_Missing_Image_Is_Refused_With_400()
    {
        var productId = await ProductApi.CreateProductAsync(Client);
        var a = (await ProductApi.ReadJsonAsync(await ProductApi.UploadImageAsync(Client, productId, ProductApi.Png)))["imageId"]!.GetValue<Guid>();
        await ProductApi.UploadImageAsync(Client, productId, ProductApi.Jpeg, "b.jpg", "image/jpeg");

        var response = await Client.PutAsJsonAsync($"/products/{productId}/images/order", new { imageIds = new[] { a } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
