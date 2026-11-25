using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using IntegrationTests.Helpers;
using Shared.Security.TestSupport;

namespace IntegrationTests.AccessControl;

/// <summary>
/// SC-001: every administrative Inventory operation refuses every caller that is not an Admin,
/// with the correct status, without changing data, and records the refusal.
/// </summary>
[Trait("Category", "AccessControl")]
public class AdminAccessMatrixTests : IntegrationTestBase
{
    public AdminAccessMatrixTests(InventoryApiFactory factory) : base(factory) { }

    public enum Caller
    {
        Anonymous,
        InvalidSignature,
        Expired,
        Customer,
        LegacyManager,
        LegacySupport
    }

    // Name, HTTP method, path template ({p} product id)
    public static IEnumerable<object[]> AdminRoutes() =>
    [
        ["list-inventory", "GET", "/inventory"],
        ["adjust-inventory", "POST", "/inventory/{p}/adjustments"],
        ["audit", "GET", "/inventory/admin/audit"]
    ];

    public static IEnumerable<object[]> RefusedCallers() =>
        Enum.GetValues<Caller>().Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(RefusedCallers))]
    public async Task Every_admin_route_refuses_non_admin_callers_and_changes_nothing(Caller caller)
    {
        var productId = await SeedStockAsync(10);
        var before = await GetStockJson(productId);

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

        (await GetStockJson(productId)).Should().Be(before, "refused calls must not change data");
    }

    [Fact]
    public async Task Admin_is_allowed_on_every_admin_route()
    {
        var productId = await SeedStockAsync(5);

        foreach (var route in AdminRoutes())
        {
            var (name, method, template) = ((string)route[0], (string)route[1], (string)route[2]);

            using var request = BuildRequest(method, template, productId, name);
            using var response = await Client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.OK, name);
        }
    }

    [Fact]
    public async Task Public_stock_read_stays_open_to_anonymous_callers()
    {
        var productId = await SeedStockAsync(3);
        using var anonymous = Factory.CreateClient();

        (await anonymous.GetAsync($"/inventory/{productId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refusals_are_recorded_in_the_audit_trail_with_the_actor_when_known()
    {
        var customerId = Guid.NewGuid();
        using var customer = Factory.CreateClient().AsCustomer(customerId);
        using var anonymous = Factory.CreateClient();

        var path = $"/inventory/{Guid.NewGuid()}/adjustments";
        var body = JsonContent.Create(new { delta = 1, reason = "should be refused" });

        (await customer.PostAsync(path, body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await anonymous.PostAsync(path, JsonContent.Create(new { delta = 1, reason = "should be refused" })))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var entries = (await AuditItems())
            .Where(e => e.GetProperty("action").GetString() == "access.denied"
                        && e.GetProperty("targetId").GetString() == $"POST {path}")
            .ToList();

        entries.Should().HaveCount(2);
        entries.Should().OnlyContain(e => e.GetProperty("outcome").GetString() == "refused");
        entries.Should().Contain(e => e.GetProperty("actorId").GetString() == customerId.ToString());
        entries.Should().Contain(e => e.GetProperty("actorId").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task Admin_adjustments_are_recorded_in_the_audit_trail()
    {
        var productId = await SeedStockAsync(7);

        var forProduct = (await AuditItems())
            .Where(e => e.GetProperty("targetId").GetString() == productId.ToString())
            .ToList();

        forProduct.Should().ContainSingle(e =>
            e.GetProperty("action").GetString() == "inventory.adjust"
            && e.GetProperty("outcome").GetString() == "success");
    }

    [Fact]
    public async Task Failed_adjustments_are_recorded_and_change_nothing()
    {
        var productId = Guid.NewGuid();

        var response = await Client.PostAsJsonAsync(
            $"/inventory/{productId}/adjustments",
            new { delta = -5, reason = "removing stock that does not exist" });

        response.IsSuccessStatusCode.Should().BeFalse();

        (await Client.GetAsync($"/inventory/{productId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await AuditItems()).Should().Contain(e =>
            e.GetProperty("targetId").GetString() == productId.ToString()
            && e.GetProperty("action").GetString() == "inventory.adjust"
            && e.GetProperty("outcome").GetString() == "failed");
    }

    private async Task<List<JsonElement>> AuditItems()
    {
        var audit = await Client.GetFromJsonAsync<JsonElement>("/inventory/admin/audit?pageSize=100");
        return audit.GetProperty("items").EnumerateArray().ToList();
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
        var request = new HttpRequestMessage(new HttpMethod(method), template.Replace("{p}", productId.ToString()));

        if (name == "adjust-inventory")
        {
            request.Content = JsonContent.Create(new { delta = 1, reason = "access matrix probe" });
        }

        return request;
    }

    private async Task<Guid> SeedStockAsync(int quantity)
    {
        var productId = Guid.NewGuid();
        var response = await Client.PostAsJsonAsync(
            $"/inventory/{productId}/adjustments",
            new { delta = quantity, reason = "access matrix seed" });
        response.EnsureSuccessStatusCode();
        return productId;
    }

    private async Task<string> GetStockJson(Guid productId)
    {
        using var response = await Client.GetAsync($"/inventory/{productId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}