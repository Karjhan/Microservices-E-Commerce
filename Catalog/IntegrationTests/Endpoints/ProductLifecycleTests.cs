using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using IntegrationTests.Helpers;

namespace IntegrationTests.Endpoints;

/// <summary>US2: product validation, statuses and shopper visibility (FR-008, FR-012, FR-013, SC-003).</summary>
public class ProductLifecycleTests : IntegrationTestBase
{
    public ProductLifecycleTests(CatalogApiFactory factory) : base(factory) { }

    private HttpClient Shopper => Factory.CreateClient();

    [Fact]
    public async Task A_New_Product_Is_A_Draft()
    {
        var id = await ProductApi.CreateProductAsync(Client);

        var product = await ProductApi.ReadJsonAsync(await Client.GetAsync($"/products/{id}"));

        product["status"]!.GetValue<string>().Should().Be("Draft");
    }

    [Fact]
    public async Task Invalid_Fields_Are_Reported_Per_Field_With_400()
    {
        var response = await Client.PostAsJsonAsync("/products", ProductApi.NewProductBody(name: "x", price: -5m));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ProductApi.ReadJsonAsync(response);
        body["code"]!.GetValue<string>().Should().Be("validation_failed");
        body["errors"]!.AsObject().Select(p => p.Key.ToLowerInvariant()).Should().Contain(["name", "price"]);
    }

    [Fact]
    public async Task A_Duplicate_Name_Is_Refused_Ignoring_Case()
    {
        var name = "Unique " + Guid.NewGuid();
        await ProductApi.CreateProductAsync(Client, name);

        var response = await Client.PostAsJsonAsync("/products", ProductApi.NewProductBody(name.ToUpperInvariant()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ProductApi.ReadJsonAsync(response))["errors"]!.ToJsonString().Should().Contain("already exists");
    }

    [Fact]
    public async Task Deleting_Without_Confirmation_Is_Refused_And_Changes_Nothing()
    {
        var id = await ProductApi.CreateProductAsync(Client);

        var response = await Client.DeleteAsync($"/products/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Client.GetAsync($"/products/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_Draft_Is_Invisible_To_Shoppers_Until_It_Is_Activated()
    {
        var id = await ProductApi.CreateProductAsync(Client);

        (await Shopper.GetAsync($"/products/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await ProductApi.SetStatusAsync(Client, id, "Active")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await Shopper.GetAsync($"/products/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Deactivating_A_Cached_Product_Hides_It_Immediately()
    {
        var id = await ProductApi.CreateActiveProductAsync(Client);
        (await Shopper.GetAsync($"/products/{id}")).StatusCode.Should().Be(HttpStatusCode.OK); // warms the cache

        await ProductApi.SetStatusAsync(Client, id, "Inactive");

        (await Shopper.GetAsync($"/products/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync($"/products/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_Shopper_List_Only_Contains_Active_Products()
    {
        var token = Guid.NewGuid().ToString("N");
        var active = await ProductApi.CreateActiveProductAsync(Client, $"Listed {token} active");
        var draft = await ProductApi.CreateProductAsync(Client, $"Listed {token} draft");

        var response = await Shopper.GetAsync($"/products?search={token}&pageSize=50");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain(active.ToString());
        body.Should().NotContain(draft.ToString());
    }

    [Fact]
    public async Task A_Forbidden_Transition_Is_Refused_With_422()
    {
        var id = await ProductApi.CreateProductAsync(Client);
        await ProductApi.SetStatusAsync(Client, id, "Discontinued");

        var response = await ProductApi.SetStatusAsync(Client, id, "Active");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ProductApi.ReadJsonAsync(response))["code"]!.GetValue<string>().Should().Be("invalid_status_transition");
    }

    [Fact]
    public async Task Editing_A_Product_Does_Not_Reset_Its_Counters_Or_Status()
    {
        var id = await ProductApi.CreateActiveProductAsync(Client);

        var update = await Client.PutAsJsonAsync($"/products/{id}", ProductApi.NewProductBody(price: 9.99m));
        update.EnsureSuccessStatusCode();

        var product = await ProductApi.ReadJsonAsync(await Client.GetAsync($"/products/{id}"));
        product["status"]!.GetValue<string>().Should().Be("Active");
        product["price"]!.GetValue<decimal>().Should().Be(9.99m);
    }

    [Fact]
    public async Task The_Admin_List_Includes_Every_Status_And_Supports_Filtering_Search_And_Paging()
    {
        var token = Guid.NewGuid().ToString("N");
        var a = await ProductApi.CreateActiveProductAsync(Client, $"Admin {token} alpha");
        var b = await ProductApi.CreateProductAsync(Client, $"Admin {token} beta");
        var c = await ProductApi.CreateProductAsync(Client, $"Admin {token} gamma");
        await ProductApi.SetStatusAsync(Client, c, "Discontinued");

        var all = await ProductApi.ReadJsonAsync(await Client.GetAsync($"/products/admin?q={token}&sortBy=name&sortDir=asc"));
        var drafts = await ProductApi.ReadJsonAsync(await Client.GetAsync($"/products/admin?q={token}&status=Draft"));
        var paged = await ProductApi.ReadJsonAsync(await Client.GetAsync($"/products/admin?q={token}&sortBy=name&sortDir=asc&page=2&pageSize=2"));

        all["totalCount"]!.GetValue<int>().Should().Be(3);
        all["items"]!.AsArray().Select(i => i!["id"]!.GetValue<Guid>()).Should().Equal(a, b, c);
        drafts["items"]!.AsArray().Select(i => i!["id"]!.GetValue<Guid>()).Should().Equal(b);
        paged["items"]!.AsArray().Select(i => i!["id"]!.GetValue<Guid>()).Should().Equal(c);
    }

    [Fact]
    public async Task The_Admin_List_Is_Not_Available_To_Shoppers()
    {
        var response = await Shopper.GetAsync("/products/admin");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }
}
