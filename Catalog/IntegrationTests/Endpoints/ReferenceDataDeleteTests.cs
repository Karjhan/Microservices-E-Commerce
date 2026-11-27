using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using IntegrationTests.Helpers;

namespace IntegrationTests.Endpoints;

/// <summary>US3: unique names, in-use protection and deactivation (FR-021, FR-023, US3 scenarios 2 and 3).</summary>
public class ReferenceDataDeleteTests : IntegrationTestBase
{
    public ReferenceDataDeleteTests(CatalogApiFactory factory) : base(factory) { }

    public static TheoryData<string> Resources => new() { "brands", "categories", "filament-types" };

    private async Task<Guid> CreateAsync(string resource, string? name = null) => resource switch
    {
        "brands" => await ReferenceDataApi.CreateBrandAsync(Client, name),
        "categories" => await ReferenceDataApi.CreateCategoryAsync(Client, name),
        _ => await ReferenceDataApi.CreateFilamentTypeAsync(Client, name)
    };

    private async Task<Guid> CreateProductUsingAsync(string resource, Guid id) => resource switch
    {
        "brands" => await ReferenceDataApi.CreateProductAsync(Client, brandId: id),
        "categories" => await ReferenceDataApi.CreateProductAsync(Client, categoryId: id),
        _ => await ReferenceDataApi.CreateProductAsync(Client, filamentTypeIds: [id])
    };

    [Theory]
    [MemberData(nameof(Resources))]
    public async Task Deleting_An_Entry_That_Products_Use_Is_Refused_With_The_Product_Count(string resource)
    {
        var id = await CreateAsync(resource);
        await CreateProductUsingAsync(resource, id);
        await CreateProductUsingAsync(resource, id);

        var response = await Client.DeleteAsync($"/{resource}/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await ProductApi.ReadJsonAsync(response);
        body["code"]!.GetValue<string>().Should().Be("in_use");
        body["details"]!["productCount"]!.GetValue<int>().Should().Be(2);
        body["detail"]!.GetValue<string>().Should().Contain("2").And.Contain("Deactivate");

        // Nothing was removed.
        var list = await ProductApi.ReadJsonAsync(await Client.GetAsync($"/{resource}?includeInactive=true"));
        list.AsArray().Select(x => x!["id"]!.GetValue<Guid>()).Should().Contain(id);
    }

    [Theory]
    [MemberData(nameof(Resources))]
    public async Task An_Unused_Entry_Can_Be_Deleted(string resource)
    {
        var id = await CreateAsync(resource);

        var response = await Client.DeleteAsync($"/{resource}/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var list = await ProductApi.ReadJsonAsync(await Client.GetAsync($"/{resource}?includeInactive=true"));
        list.AsArray().Select(x => x!["id"]!.GetValue<Guid>()).Should().NotContain(id);
    }

    [Theory]
    [MemberData(nameof(Resources))]
    public async Task A_Duplicate_Name_Is_Refused_Ignoring_Case_And_Spacing(string resource)
    {
        var name = ReferenceDataApi.UniqueName("Dup");
        await CreateAsync(resource, name);

        var response = await Client.PostAsJsonAsync($"/{resource}", new { name = "  " + name.ToUpperInvariant() + " " });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await ProductApi.ReadJsonAsync(response);
        body["code"]!.GetValue<string>().Should().Be("duplicate_name");
        body["errors"]!["name"]!.ToJsonString().Should().Contain("unique");
    }

    [Theory]
    [MemberData(nameof(Resources))]
    public async Task Renaming_To_Another_Entrys_Name_Is_Refused(string resource)
    {
        var first = await CreateAsync(resource);
        var secondName = ReferenceDataApi.UniqueName("Second");
        await CreateAsync(resource, secondName);

        var response = await Client.PutAsJsonAsync($"/{resource}/{first}", new { name = secondName.ToLowerInvariant() });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ProductApi.ReadJsonAsync(response))["code"]!.GetValue<string>().Should().Be("duplicate_name");
    }

    [Fact]
    public async Task Deactivated_Entries_Stay_On_Existing_Products_But_Cannot_Be_Assigned_Again()
    {
        var brand = await ReferenceDataApi.CreateBrandAsync(Client);
        var filament = await ReferenceDataApi.CreateFilamentTypeAsync(Client);
        var category = await ReferenceDataApi.CreateCategoryAsync(Client);

        var productId = await ReferenceDataApi.CreateProductAsync(Client, category, brand, [filament]);

        foreach (var (resource, id) in new[] { ("brands", brand), ("filament-types", filament), ("categories", category) })
            (await ReferenceDataApi.SetActiveAsync(Client, resource, id, false)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The product still shows all three.
        var product = await ProductApi.ReadJsonAsync(await Client.GetAsync($"/products/{productId}"));
        product["brand"]!["id"]!.GetValue<Guid>().Should().Be(brand);
        product["filamentTypes"]!.AsArray().Should().ContainSingle();
        product["categoryId"]!.GetValue<Guid>().Should().Be(category);

        // Saving the product again with the same entries is fine.
        var update = await Client.PutAsJsonAsync($"/products/{productId}", new
        {
            Name = "Renamed " + Guid.NewGuid(),
            ShortDescription = "S",
            LongDescription = "L",
            Price = 2m,
            Currency = "EUR",
            Settings = new { },
            Size = new { },
            CategoryId = category,
            BrandId = brand,
            FilamentTypeIds = new[] { filament }
        });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // A new product cannot pick any of them.
        foreach (var body in new[]
                 {
                     ReferenceDataApi.ProductBody(brandId: brand),
                     ReferenceDataApi.ProductBody(filamentTypeIds: [filament]),
                     ReferenceDataApi.ProductBody(categoryId: category)
                 })
        {
            var response = await Client.PostAsJsonAsync("/products", body);

            response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await ProductApi.ReadJsonAsync(response))["code"]!.GetValue<string>().Should().Be("reference_inactive");
        }

        // They are hidden from shoppers, and an admin can still list them.
        var publicList = await ProductApi.ReadJsonAsync(await Factory.CreateClient().GetAsync("/brands"));
        publicList.AsArray().Select(x => x!["id"]!.GetValue<Guid>()).Should().NotContain(brand);

        var adminList = await ProductApi.ReadJsonAsync(await Client.GetAsync("/brands?includeInactive=true"));
        var entry = adminList.AsArray().Single(x => x!["id"]!.GetValue<Guid>() == brand)!;
        entry["isActive"]!.GetValue<bool>().Should().BeFalse();
        entry["productCount"]!.GetValue<int>().Should().Be(1);

        // Reactivating makes it available again.
        (await ReferenceDataApi.SetActiveAsync(Client, "brands", brand, true)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.PostAsJsonAsync("/products", ReferenceDataApi.ProductBody(brandId: brand)))
            .StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
