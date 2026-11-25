using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using IntegrationTests.Helpers;
using Shared.Security.TestSupport;

namespace IntegrationTests.AccessControl;

/// <summary>
/// SC-001: every administrative Catalog operation refuses every caller that is not an Admin,
/// with the correct status, without changing data, and records the refusal.
/// </summary>
[Trait("Category", "AccessControl")]
public class AdminAccessMatrixTests : IntegrationTestBase
{
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    public AdminAccessMatrixTests(CatalogApiFactory factory) : base(factory) { }

    public enum Caller
    {
        Anonymous,
        InvalidSignature,
        Expired,
        Customer,
        LegacyManager,
        LegacySupport
    }

    // Name, HTTP method, path template ({p} product id, {x} any other id)
    public static IEnumerable<object[]> AdminRoutes() =>
    [
        ["create-product", "POST", "/products"],
        ["update-product", "PUT", "/products/{p}"],
        ["delete-product", "DELETE", "/products/{p}"],
        ["upload-image", "POST", "/products/{p}/images"],
        ["delete-image", "DELETE", "/products/{p}/images/{x}"],
        ["add-attribute", "POST", "/products/{p}/attributes"],
        ["delete-attribute", "DELETE", "/products/{p}/attributes/{x}"],
        ["add-relation", "POST", "/products/{p}/relations"],
        ["delete-relation", "DELETE", "/products/{p}/relations/{x}"],
        ["audit", "GET", "/products/admin/audit"]
    ];

    public static IEnumerable<object[]> RefusedCallers() =>
        Enum.GetValues<Caller>().Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(RefusedCallers))]
    public async Task Every_admin_route_refuses_non_admin_callers_and_changes_nothing(Caller caller)
    {
        var productId = await CreateProductAsAdmin($"matrix-{caller}-{Guid.NewGuid():N}");
        var before = await GetProductJson(productId);

        var (expectedStatus, expectedCode) = caller switch
        {
            Caller.Anonymous or Caller.InvalidSignature or Caller.Expired
                => (HttpStatusCode.Unauthorized, "unauthenticated"),
            _ => (HttpStatusCode.Forbidden, "forbidden")
        };

        foreach (var route in AdminRoutes())
        {
            var (name, method, template) = ((string)route[0], (string)route[1], (string)route[2]);

            using var client = CreateClient(caller);
            using var request = BuildRequest(method, template, productId, name);
            using var response = await client.SendAsync(request);

            response.StatusCode.Should().Be(expectedStatus, $"{name} as {caller}");

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("code").GetString().Should().Be(expectedCode, $"{name} as {caller}");

            if (expectedStatus == HttpStatusCode.Unauthorized)
            {
                response.Headers.WwwAuthenticate.Should().Contain(h => h.Scheme == "Bearer", $"{name} as {caller}");
            }
        }

        (await GetProductJson(productId)).Should().Be(before, "refused calls must not change data");
    }

    [Fact]
    public async Task Admin_is_allowed_on_every_admin_route()
    {
        foreach (var route in AdminRoutes())
        {
            var (name, method, template) = ((string)route[0], (string)route[1], (string)route[2]);
            var productId = await CreateProductAsAdmin($"matrix-admin-{name}-{Guid.NewGuid():N}");

            using var request = BuildRequest(method, template, productId, name);
            using var response = await Client.SendAsync(request);

            response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized, name);
            response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden, name);
        }
    }

    [Fact]
    public async Task Public_reads_stay_open_to_anonymous_callers()
    {
        var productId = await CreateProductAsAdmin($"matrix-public-{Guid.NewGuid():N}");
        using var anonymous = Factory.CreateClient();

        (await anonymous.GetAsync("/products?page=1&pageSize=20")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await anonymous.GetAsync($"/products/{productId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await anonymous.GetAsync($"/products/{productId}/attributes")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await anonymous.GetAsync($"/products/{productId}/relations")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refusals_are_recorded_in_the_audit_trail_with_the_actor_when_known()
    {
        var customerId = Guid.NewGuid();
        using var customer = Factory.CreateClient().AsCustomer(customerId);
        using var anonymous = Factory.CreateClient();

        var path = $"/products/{Guid.NewGuid()}";
        (await customer.DeleteAsync(path)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await anonymous.DeleteAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var audit = await Client.GetFromJsonAsync<JsonElement>("/products/admin/audit?pageSize=100");
        var entries = audit.GetProperty("items").EnumerateArray()
            .Where(e => e.GetProperty("action").GetString() == "access.denied"
                        && e.GetProperty("targetId").GetString() == $"DELETE {path}")
            .ToList();

        entries.Should().HaveCount(2);
        entries.Should().OnlyContain(e => e.GetProperty("outcome").GetString() == "refused");
        entries.Should().Contain(e => e.GetProperty("actorId").GetString() == customerId.ToString());
        entries.Should().Contain(e => e.GetProperty("actorId").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task Admin_changes_are_recorded_in_the_audit_trail()
    {
        var name = $"audited-{Guid.NewGuid():N}";
        var productId = await CreateProductAsAdmin(name);

        var delete = await Client.DeleteAsync($"/products/{productId}");
        delete.EnsureSuccessStatusCode();

        var audit = await Client.GetFromJsonAsync<JsonElement>("/products/admin/audit?pageSize=100");
        var forProduct = audit.GetProperty("items").EnumerateArray()
            .Where(e => e.GetProperty("targetId").GetString() == productId.ToString())
            .Select(e => e.GetProperty("action").GetString())
            .ToList();

        forProduct.Should().Contain("product.create");
        forProduct.Should().Contain("product.delete");
    }

    private HttpClient CreateClient(Caller caller)
    {
        var client = Factory.CreateClient();
        return caller switch
        {
            Caller.Anonymous => client,
            Caller.InvalidSignature => client.WithBearer(TestTokens.WrongSignature()),
            Caller.Expired => client.WithBearer(TestTokens.Expired()),
            Caller.Customer => client.AsCustomer(),
            Caller.LegacyManager => client.WithBearer(TestTokens.Create("Manager")),
            Caller.LegacySupport => client.WithBearer(TestTokens.Create("Support")),
            _ => throw new ArgumentOutOfRangeException(nameof(caller))
        };
    }

    private static HttpRequestMessage BuildRequest(string method, string template, Guid productId, string name)
    {
        var path = template.Replace("{p}", productId.ToString()).Replace("{x}", Guid.NewGuid().ToString());
        var request = new HttpRequestMessage(new HttpMethod(method), path);

        switch (name)
        {
            case "create-product":
                request.Content = JsonBody(ProductBody($"matrix-created-{Guid.NewGuid():N}"));
                break;
            case "update-product":
                request.Content = JsonBody(ProductBody("matrix-updated"));
                break;
            case "upload-image":
                var file = new ByteArrayContent(TinyPng);
                file.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
                request.Content = new MultipartFormDataContent { { file, "file", "pixel.png" }, { new StringContent("true"), "isPrimary" } };
                break;
            case "add-attribute":
                request.Content = JsonBody(new { key = "Color", value = "Red" });
                break;
            case "add-relation":
                request.Content = JsonBody(new { relatedProductId = Guid.NewGuid(), type = "Accessory" });
                break;
        }

        return request;
    }

    private static StringContent JsonBody(object body) =>
        new(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");

    private static object ProductBody(string name) => new
    {
        Name = name,
        ShortDescription = "Short",
        LongDescription = "Long",
        Price = 12.5m,
        CategoryId = Guid.NewGuid(),
        Currency = "EUR",
        Settings = new { },
        Size = new { }
    };

    private async Task<Guid> CreateProductAsAdmin(string name)
    {
        var response = await Client.PostAsJsonAsync("/products", ProductBody(name));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductResponse>())!.ProductId;
    }

    private async Task<string> GetProductJson(Guid productId)
    {
        using var response = await Client.GetAsync($"/products/{productId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}