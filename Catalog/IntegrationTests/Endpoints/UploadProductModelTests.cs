using System.Net;
using FluentAssertions;
using IntegrationTests.Helpers;

namespace IntegrationTests.Endpoints;

/// <summary>US2: one 3D model per product, up to 100 MB, streamed to private storage (FR-011, SC-009).</summary>
public class UploadProductModelTests : IntegrationTestBase
{
    public UploadProductModelTests(CatalogApiFactory factory) : base(factory) { }

    private static byte[] Bytes(int size) => new byte[size];

    [Fact]
    public async Task A_Glb_Is_Stored_And_Reported_As_Previewable()
    {
        var productId = await ProductApi.CreateProductAsync(Client);

        var response = await ProductApi.UploadModelAsync(Client, productId, Bytes(1024), "part.glb");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var model = await ProductApi.ReadJsonAsync(response);
        model["format"]!.GetValue<string>().Should().Be("glb");
        model["isPreviewable"]!.GetValue<bool>().Should().BeTrue();
        (await Factory.ListKeysAsync(CatalogApiFactory.ModelBucket, $"products/{productId}/model/")).Should().HaveCount(1);
    }

    [Theory]
    [InlineData("print.3mf")]
    [InlineData("shape.obj")]
    public async Task Stored_Only_Formats_Are_Accepted_But_Not_Previewable(string fileName)
    {
        var productId = await ProductApi.CreateProductAsync(Client);

        var response = await ProductApi.UploadModelAsync(Client, productId, Bytes(512), fileName);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ProductApi.ReadJsonAsync(response))["isPreviewable"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task An_Unsupported_Extension_Is_Refused_With_415()
    {
        var productId = await ProductApi.CreateProductAsync(Client);

        var response = await ProductApi.UploadModelAsync(Client, productId, Bytes(512), "script.exe");

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        (await Factory.ListKeysAsync(CatalogApiFactory.ModelBucket, $"products/{productId}/")).Should().BeEmpty();
    }

    [Fact]
    public async Task A_Model_Larger_Than_The_Default_Body_Limit_Is_Accepted()
    {
        var productId = await ProductApi.CreateProductAsync(Client);

        var response = await ProductApi.UploadModelAsync(Client, productId, Bytes(31 * 1024 * 1024), "big.stl");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ProductApi.ReadJsonAsync(response))["sizeBytes"]!.GetValue<long>().Should().Be(31L * 1024 * 1024);
    }

    [Fact]
    public async Task A_Model_Over_One_Hundred_Megabytes_Is_Refused_With_413()
    {
        var productId = await ProductApi.CreateProductAsync(Client);

        var response = await ProductApi.UploadModelAsync(Client, productId, Bytes(100 * 1024 * 1024 + 1), "huge.glb");

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        (await Factory.ListKeysAsync(CatalogApiFactory.ModelBucket, $"products/{productId}/")).Should().BeEmpty();
    }

    [Fact]
    public async Task Replacing_The_Model_Keeps_One_File_And_Returns_200()
    {
        var productId = await ProductApi.CreateProductAsync(Client);
        await ProductApi.UploadModelAsync(Client, productId, Bytes(100), "v1.glb");

        var response = await ProductApi.UploadModelAsync(Client, productId, Bytes(200), "v2.stl");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var keys = await Factory.ListKeysAsync(CatalogApiFactory.ModelBucket, $"products/{productId}/model/");
        keys.Should().ContainSingle().Which.Should().EndWith(".stl");
    }

    [Fact]
    public async Task The_Model_Link_Is_A_Working_Presigned_Url()
    {
        var productId = await ProductApi.CreateActiveProductAsync(Client);
        await ProductApi.UploadModelAsync(Client, productId, [1, 2, 3, 4], "tiny.glb");

        var response = await Factory.CreateClient().GetAsync($"/products/{productId}/model");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var url = (await ProductApi.ReadJsonAsync(response))["url"]!.GetValue<string>();
        using var http = new HttpClient();
        (await http.GetByteArrayAsync(url)).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public async Task The_Model_Of_A_Draft_Is_Hidden_From_Shoppers()
    {
        var productId = await ProductApi.CreateProductAsync(Client);
        await ProductApi.UploadModelAsync(Client, productId, Bytes(10), "part.glb");

        var shopper = await Factory.CreateClient().GetAsync($"/products/{productId}/model");
        var admin = await Client.GetAsync($"/products/{productId}/model");

        shopper.StatusCode.Should().Be(HttpStatusCode.NotFound);
        admin.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Deleting_The_Model_Removes_The_File()
    {
        var productId = await ProductApi.CreateProductAsync(Client);
        await ProductApi.UploadModelAsync(Client, productId, Bytes(10), "part.glb");

        var response = await Client.DeleteAsync($"/products/{productId}/model");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Factory.ListKeysAsync(CatalogApiFactory.ModelBucket, $"products/{productId}/")).Should().BeEmpty();
    }

    [Fact]
    public async Task Deleting_The_Product_Removes_Its_Images_And_Model()
    {
        var productId = await ProductApi.CreateProductAsync(Client);
        await ProductApi.UploadModelAsync(Client, productId, Bytes(10), "part.glb");
        await ProductApi.UploadImageAsync(Client, productId, ProductApi.Png);

        var response = await Client.DeleteAsync($"/products/{productId}?confirm=true");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Factory.ListKeysAsync(CatalogApiFactory.ModelBucket, $"products/{productId}/")).Should().BeEmpty();
        (await Factory.ListKeysAsync(CatalogApiFactory.ImageBucket, $"products/{productId}/")).Should().BeEmpty();
    }
}
