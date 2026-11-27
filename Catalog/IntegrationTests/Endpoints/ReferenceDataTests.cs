using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using IntegrationTests.Helpers;
using Shared.Security.TestSupport;

namespace IntegrationTests.Endpoints;

/// <summary>US3: listing, product classification, filters and brand logos (FR-021, FR-022, FR-024).</summary>
public class ReferenceDataTests : IntegrationTestBase
{
    public ReferenceDataTests(CatalogApiFactory factory) : base(factory) { }

    private HttpClient Shopper => Factory.CreateClient();

    private static IEnumerable<Guid> Ids(JsonNode list) => list.AsArray().Select(x => x!["id"]!.GetValue<Guid>());

    [Theory]
    [InlineData("brands")]
    [InlineData("categories")]
    [InlineData("filament-types")]
    public async Task Inactive_Entries_Are_Only_Listed_For_Admins(string resource)
    {
        var id = resource switch
        {
            "brands" => await ReferenceDataApi.CreateBrandAsync(Client),
            "categories" => await ReferenceDataApi.CreateCategoryAsync(Client),
            _ => await ReferenceDataApi.CreateFilamentTypeAsync(Client)
        };
        (await ReferenceDataApi.SetActiveAsync(Client, resource, id, false)).EnsureSuccessStatusCode();

        // Shoppers: active entries only, without usage counts.
        var publicList = await ProductApi.ReadJsonAsync(await Shopper.GetAsync($"/{resource}"));
        Ids(publicList).Should().NotContain(id);
        publicList.AsArray().Should().OnlyContain(x => x!["isActive"]!.GetValue<bool>() && x["productCount"] == null);

        // Asking for inactive ones needs a token, and an admin one.
        (await Shopper.GetAsync($"/{resource}?includeInactive=true")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var customer = Factory.CreateClient().AsCustomer();
        (await customer.GetAsync($"/{resource}?includeInactive=true")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var adminList = await ProductApi.ReadJsonAsync(await Client.GetAsync($"/{resource}?includeInactive=true"));
        Ids(adminList).Should().Contain(id);
    }

    [Fact]
    public async Task A_Product_Shows_Its_Brand_Filament_Types_And_Category()
    {
        var brand = await ReferenceDataApi.CreateBrandAsync(Client, ReferenceDataApi.UniqueName("Prusa"));
        var pla = await ReferenceDataApi.CreateFilamentTypeAsync(Client, code: "Test" + Guid.NewGuid().ToString("N")[..8]);
        var category = await ReferenceDataApi.CreateCategoryAsync(Client);

        var productId = await ReferenceDataApi.CreateProductAsync(Client, category, brand, [pla, pla]);

        var product = await ProductApi.ReadJsonAsync(await Client.GetAsync($"/products/{productId}"));

        product["brand"]!["id"]!.GetValue<Guid>().Should().Be(brand);
        product["categoryName"]!.GetValue<string>().Should().NotBeNullOrEmpty();
        product["filamentTypes"]!.AsArray().Should().ContainSingle();

        var code = product["filamentTypes"]![0]!["code"]!.GetValue<string>();
        product["supportedMaterials"]!.AsArray().Select(x => x!.GetValue<string>()).Should().Equal(code);
    }

    [Fact]
    public async Task A_Product_Needs_An_Existing_Category_Brand_And_Filament_Types()
    {
        var unknownCategory = await Client.PostAsJsonAsync("/products", ReferenceDataApi.ProductBody(categoryId: Guid.NewGuid()));
        unknownCategory.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ProductApi.ReadJsonAsync(unknownCategory))["errors"]!.ToJsonString().ToLowerInvariant().Should().Contain("categoryid");

        var unknownBrand = await Client.PostAsJsonAsync("/products", ReferenceDataApi.ProductBody(brandId: Guid.NewGuid()));
        unknownBrand.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var unknownFilament = await Client.PostAsJsonAsync(
            "/products", ReferenceDataApi.ProductBody(filamentTypeIds: [Guid.NewGuid()]));
        unknownFilament.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Updating_A_Product_Can_Change_And_Clear_Its_Classification()
    {
        var brand = await ReferenceDataApi.CreateBrandAsync(Client);
        var pla = await ReferenceDataApi.CreateFilamentTypeAsync(Client);
        var petg = await ReferenceDataApi.CreateFilamentTypeAsync(Client);
        var category = await ReferenceDataApi.CreateCategoryAsync(Client);

        var productId = await ReferenceDataApi.CreateProductAsync(Client, brandId: brand, filamentTypeIds: [pla]);

        object Update(Guid? categoryId, Guid? brandId, Guid[]? filamentTypeIds) => new
        {
            Name = "Updated " + productId,
            ShortDescription = "S",
            LongDescription = "L",
            Price = 3m,
            Currency = "EUR",
            Settings = new { },
            Size = new { },
            CategoryId = categoryId,
            BrandId = brandId,
            FilamentTypeIds = filamentTypeIds
        };

        // Change category and filament types; the brand is sent as null, so it is removed.
        (await Client.PutAsJsonAsync($"/products/{productId}", Update(category, null, [pla, petg])))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var product = await ProductApi.ReadJsonAsync(await Client.GetAsync($"/products/{productId}"));
        product["categoryId"]!.GetValue<Guid>().Should().Be(category);
        product["brand"].Should().BeNull();
        product["filamentTypes"]!.AsArray().Should().HaveCount(2);

        // Leaving the filament types out keeps them.
        (await Client.PutAsJsonAsync($"/products/{productId}", Update(null, null, null)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        product = await ProductApi.ReadJsonAsync(await Client.GetAsync($"/products/{productId}"));
        product["categoryId"]!.GetValue<Guid>().Should().Be(category);
        product["filamentTypes"]!.AsArray().Should().HaveCount(2);

        // An empty list clears them.
        (await Client.PutAsJsonAsync($"/products/{productId}", Update(null, null, [])))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        product = await ProductApi.ReadJsonAsync(await Client.GetAsync($"/products/{productId}"));
        product["filamentTypes"]!.AsArray().Should().BeEmpty();
    }

    [Fact]
    public async Task Renaming_An_Entry_Updates_Products_That_Are_Already_Cached()
    {
        var brand = await ReferenceDataApi.CreateBrandAsync(Client, ReferenceDataApi.UniqueName("Before"));
        var productId = await ReferenceDataApi.CreateProductAsync(Client, brandId: brand);

        // Reading twice makes sure the detail is cached.
        await Client.GetAsync($"/products/{productId}");
        var cached = await ProductApi.ReadJsonAsync(await Client.GetAsync($"/products/{productId}"));
        cached["brand"]!["name"]!.GetValue<string>().Should().StartWith("Before");

        var newName = ReferenceDataApi.UniqueName("After");
        (await Client.PutAsJsonAsync($"/brands/{brand}", new { name = newName })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var product = await ProductApi.ReadJsonAsync(await Client.GetAsync($"/products/{productId}"));
        product["brand"]!["name"]!.GetValue<string>().Should().Be(newName);
    }

    [Fact]
    public async Task Products_Can_Be_Filtered_By_Brand_And_Filament_Type_Code()
    {
        var brand = await ReferenceDataApi.CreateBrandAsync(Client);
        var otherBrand = await ReferenceDataApi.CreateBrandAsync(Client);
        var code = "Flt" + Guid.NewGuid().ToString("N")[..10];
        var filament = await ReferenceDataApi.CreateFilamentTypeAsync(Client, code: code);

        var match = await CreateActiveAsync(brand, [filament]);
        var wrongBrand = await CreateActiveAsync(otherBrand, [filament]);
        var noFilament = await CreateActiveAsync(brand, null);
        var draft = await ReferenceDataApi.CreateProductAsync(Client, brandId: brand, filamentTypeIds: [filament]);

        async Task<List<Guid>> Shop(string query)
        {
            var list = await ProductApi.ReadJsonAsync(await Shopper.GetAsync($"/products?pageSize=100&{query}"));
            return Ids(list["items"]!).ToList();
        }

        // Both filters together.
        (await Shop($"brandId={brand}&filamentType={code.ToLowerInvariant()}")).Should().Equal(match);

        // Each on its own.
        (await Shop($"brandId={brand}")).Should().BeEquivalentTo([match, noFilament]);
        (await Shop($"filamentType={code}")).Should().BeEquivalentTo([match, wrongBrand]);

        // The old 'material' name still works.
        (await Shop($"material={code}")).Should().BeEquivalentTo([match, wrongBrand]);

        // The admin list filters the same way and also sees drafts.
        var admin = await ProductApi.ReadJsonAsync(
            await Client.GetAsync($"/products/admin?brandId={brand}&filamentType={code}&pageSize=100"));
        Ids(admin["items"]!).Should().BeEquivalentTo([match, draft]);
    }

    private async Task<Guid> CreateActiveAsync(Guid brandId, Guid[]? filamentTypeIds)
    {
        var id = await ReferenceDataApi.CreateProductAsync(Client, brandId: brandId, filamentTypeIds: filamentTypeIds);
        (await ProductApi.SetStatusAsync(Client, id, "Active")).EnsureSuccessStatusCode();
        return id;
    }

    [Fact]
    public async Task A_Filament_Type_Rejects_An_Invalid_Diameter_And_A_Reused_Code()
    {
        var invalid = await Client.PostAsJsonAsync("/filament-types",
            new { name = ReferenceDataApi.UniqueName("Odd"), diameterMm = 0 });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ProductApi.ReadJsonAsync(invalid))["errors"]!.ToJsonString().Should().Contain("diameterMm");

        var code = "Code" + Guid.NewGuid().ToString("N")[..10];
        await ReferenceDataApi.CreateFilamentTypeAsync(Client, code: code);

        var reused = await Client.PostAsJsonAsync("/filament-types",
            new { name = ReferenceDataApi.UniqueName("Another"), code = code.ToUpperInvariant() });
        reused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ProductApi.ReadJsonAsync(reused))["code"]!.GetValue<string>().Should().Be("duplicate_code");
    }

    [Fact]
    public async Task A_Filament_Types_Code_Is_Derived_From_Its_Name_And_Does_Not_Change_On_Update()
    {
        var name = "Silk" + Guid.NewGuid().ToString("N")[..8] + " PLA";
        var id = await ReferenceDataApi.CreateFilamentTypeAsync(Client, name);

        (await Client.PutAsJsonAsync($"/filament-types/{id}",
            new { name = ReferenceDataApi.UniqueName("Renamed"), diameterMm = 2.85m, colorOptions = new[] { "Gold", "gold", "Silver" } }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await ProductApi.ReadJsonAsync(await Client.GetAsync("/filament-types?includeInactive=true"));
        var entry = list.AsArray().Single(x => x!["id"]!.GetValue<Guid>() == id)!;

        entry["code"]!.GetValue<string>().Should().Be(name.Replace(" ", ""));
        entry["diameterMm"]!.GetValue<decimal>().Should().Be(2.85m);
        entry["colorOptions"]!.AsArray().Select(x => x!.GetValue<string>()).Should().Equal("Gold", "Silver");
    }

    [Fact]
    public async Task A_Brand_Logo_Is_Stored_Shown_On_Products_And_Replaced_Without_Leaving_Files_Behind()
    {
        var brand = await ReferenceDataApi.CreateBrandAsync(Client);
        var productId = await ReferenceDataApi.CreateProductAsync(Client, brandId: brand);

        var first = await ReferenceDataApi.UploadLogoAsync(Client, brand, ProductApi.Png);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstUrl = (await ProductApi.ReadJsonAsync(first))["logoUrl"]!.GetValue<string>();

        (await Factory.ListKeysAsync(CatalogApiFactory.ImageBucket, $"brands/{brand}/")).Should().ContainSingle();

        var product = await ProductApi.ReadJsonAsync(await Client.GetAsync($"/products/{productId}"));
        product["brand"]!["logoUrl"]!.GetValue<string>().Should().Be(firstUrl);

        // A new logo replaces the old file.
        var second = await ReferenceDataApi.UploadLogoAsync(Client, brand, ProductApi.Jpeg, "logo.jpg", "image/jpeg");
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        (await Factory.ListKeysAsync(CatalogApiFactory.ImageBucket, $"brands/{brand}/")).Should().ContainSingle();

        var list = await ProductApi.ReadJsonAsync(await Client.GetAsync("/brands"));
        list.AsArray().Single(x => x!["id"]!.GetValue<Guid>() == brand)!["logoUrl"]!.GetValue<string>()
            .Should().NotBe(firstUrl);

        // Removing it clears the file and the link.
        (await Client.DeleteAsync($"/brands/{brand}/logo")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await Factory.ListKeysAsync(CatalogApiFactory.ImageBucket, $"brands/{brand}/")).Should().BeEmpty();
        product = await ProductApi.ReadJsonAsync(await Client.GetAsync($"/products/{productId}"));
        product["brand"]!["logoUrl"].Should().BeNull();
    }

    [Fact]
    public async Task A_Brand_Logo_Must_Be_A_Real_Image()
    {
        var brand = await ReferenceDataApi.CreateBrandAsync(Client);

        var response = await ReferenceDataApi.UploadLogoAsync(Client, brand, ProductApi.Executable, "logo.png");

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        (await Factory.ListKeysAsync(CatalogApiFactory.ImageBucket, $"brands/{brand}/")).Should().BeEmpty();
    }

    [Fact]
    public async Task Deleting_A_Brand_Removes_Its_Logo_File()
    {
        var brand = await ReferenceDataApi.CreateBrandAsync(Client);
        (await ReferenceDataApi.UploadLogoAsync(Client, brand, ProductApi.Png)).EnsureSuccessStatusCode();

        (await Client.DeleteAsync($"/brands/{brand}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await Factory.ListKeysAsync(CatalogApiFactory.ImageBucket, $"brands/{brand}/")).Should().BeEmpty();
    }

    [Fact]
    public async Task Reference_Data_Changes_Are_Audited()
    {
        var brand = await ReferenceDataApi.CreateBrandAsync(Client);
        await Client.PutAsJsonAsync($"/brands/{brand}", new { name = ReferenceDataApi.UniqueName("Audited") });
        await ReferenceDataApi.SetActiveAsync(Client, "brands", brand, false);
        await Client.DeleteAsync($"/brands/{brand}");

        var audit = await Client.GetFromJsonAsync<JsonNode>("/products/admin/audit?pageSize=100");
        var actions = audit!["items"]!.AsArray()
            .Where(e => e!["targetId"]?.GetValue<string>() == brand.ToString())
            .Select(e => e!["action"]!.GetValue<string>())
            .ToList();

        actions.Should().Contain(["brand.create", "brand.update", "brand.deactivate", "brand.delete"]);
    }
}
