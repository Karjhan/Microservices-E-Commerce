using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using IntegrationTests.Helpers;
using Shared.Security.TestSupport;

namespace IntegrationTests.AccessControl;

/// <summary>
/// SC-001: Orders refuses every caller that is not allowed — administrative routes are Admin only,
/// owner routes are owner-or-Admin and a non-owner gets 404 so existence is not revealed.
/// </summary>
[Trait("Category", "AccessControl")]
public class AdminAccessMatrixTests : IntegrationTestBase
{
    public AdminAccessMatrixTests(OrdersApiFactory factory) : base(factory) { }

    public enum Caller
    {
        Anonymous,
        InvalidSignature,
        Expired,
        Customer,
        LegacyManager,
        LegacySupport
    }

    // Admin-only: Name, method, path template
    public static IEnumerable<object[]> AdminRoutes() =>
    [
        ["list-orders", "GET", "/orders"],
        ["audit", "GET", "/orders/admin/audit"]
    ];

    // Owner-or-admin: Name, method, path template ({o} order id, {u} owner id)
    public static IEnumerable<object[]> OwnerRoutes() =>
    [
        ["get-order", "GET", "/orders/{o}"],
        ["cancel-order", "POST", "/orders/{o}/cancel"],
        ["update-address", "PUT", "/orders/{o}/shipping-address"],
        ["my-orders", "GET", "/orders/user/{u}"]
    ];

    public static IEnumerable<object[]> AllCallers() =>
        Enum.GetValues<Caller>().Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(AllCallers))]
    public async Task Admin_routes_refuse_non_admin_callers(Caller caller)
    {
        var (expectedStatus, expectedCode) = ExpectedRefusal(caller);

        foreach (var route in AdminRoutes())
        {
            var (name, method, template) = ((string)route[0], (string)route[1], (string)route[2]);

            using var client = CreateClient(caller);
            using var request = BuildRequest(method, template, Guid.NewGuid(), Guid.NewGuid(), name);
            using var response = await client.SendAsync(request);

            response.StatusCode.Should().Be(expectedStatus, $"{name} as {caller}");

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("code").GetString().Should().Be(expectedCode, $"{name} as {caller}");

            if (expectedStatus == HttpStatusCode.Unauthorized)
            {
                response.Headers.WwwAuthenticate.Should().Contain(h => h.Scheme == "Bearer", $"{name} as {caller}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllCallers))]
    public async Task Owner_routes_refuse_everyone_but_the_owner_and_admins_and_change_nothing(Caller caller)
    {
        var ownerId = Guid.NewGuid();
        var orderId = await CreateOrderAsync(ownerId);
        var before = await GetOrderJson(orderId);

        // Signed-out callers get 401; signed-in non-owners get 404 (never 403) so existence is not revealed.
        var expectedStatus = caller is Caller.Anonymous or Caller.InvalidSignature or Caller.Expired
            ? HttpStatusCode.Unauthorized
            : HttpStatusCode.NotFound;

        foreach (var route in OwnerRoutes())
        {
            var (name, method, template) = ((string)route[0], (string)route[1], (string)route[2]);

            using var client = CreateClient(caller);
            using var request = BuildRequest(method, template, orderId, ownerId, name);
            using var response = await client.SendAsync(request);

            response.StatusCode.Should().Be(expectedStatus, $"{name} as {caller}");
        }

        (await GetOrderJson(orderId)).Should().Be(before, "refused calls must not change the order");
    }

    [Fact]
    public async Task Owner_can_read_cancel_and_edit_their_own_order()
    {
        var ownerId = Guid.NewGuid();
        var orderId = await CreateOrderAsync(ownerId);
        using var owner = Factory.CreateClient().AsCustomer(ownerId);

        (await owner.GetAsync($"/orders/{orderId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.GetAsync($"/orders/user/{ownerId}")).StatusCode.Should().Be(HttpStatusCode.OK);

        using var update = BuildRequest("PUT", "/orders/{o}/shipping-address", orderId, ownerId, "update-address");
        (await owner.SendAsync(update)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var cancel = BuildRequest("POST", "/orders/{o}/cancel", orderId, ownerId, "cancel-order");
        (await owner.SendAsync(cancel)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Admin_is_allowed_on_every_route()
    {
        var ownerId = Guid.NewGuid();

        foreach (var route in AdminRoutes().Concat(OwnerRoutes()))
        {
            var (name, method, template) = ((string)route[0], (string)route[1], (string)route[2]);
            var orderId = await CreateOrderAsync(ownerId);

            using var request = BuildRequest(method, template, orderId, ownerId, name);
            using var response = await Client.SendAsync(request);

            response.IsSuccessStatusCode.Should().BeTrue($"{name} as admin returned {(int)response.StatusCode}");
        }
    }

    [Fact]
    public async Task Refusals_are_recorded_in_the_audit_trail_with_the_actor_when_known()
    {
        var customerId = Guid.NewGuid();
        using var customer = Factory.CreateClient().AsCustomer(customerId);
        using var anonymous = Factory.CreateClient();

        var path = $"/orders/admin/audit?marker={Guid.NewGuid():N}";
        (await customer.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await anonymous.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var entries = (await AuditItems())
            .Where(e => e.GetProperty("action").GetString() == "access.denied"
                        && e.GetProperty("targetId").GetString() == "GET /orders/admin/audit")
            .ToList();

        entries.Should().Contain(e => e.GetProperty("outcome").GetString() == "refused"
                                      && e.GetProperty("actorId").GetString() == customerId.ToString());
        entries.Should().Contain(e => e.GetProperty("outcome").GetString() == "refused"
                                      && e.GetProperty("actorId").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task Cancellations_are_recorded_and_non_owner_attempts_are_recorded_as_failed()
    {
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        var mine = await CreateOrderAsync(ownerId);
        var other = await CreateOrderAsync(ownerId);

        using var attacker = Factory.CreateClient().AsCustomer(attackerId);
        using var denied = BuildRequest("POST", "/orders/{o}/cancel", other, ownerId, "cancel-order");
        (await attacker.SendAsync(denied)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var allowed = BuildRequest("POST", "/orders/{o}/cancel", mine, ownerId, "cancel-order");
        (await Client.SendAsync(allowed)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var entries = await AuditItems();

        entries.Should().Contain(e => e.GetProperty("targetId").GetString() == mine.ToString()
                                      && e.GetProperty("action").GetString() == "order.cancel"
                                      && e.GetProperty("outcome").GetString() == "success");

        entries.Should().Contain(e => e.GetProperty("targetId").GetString() == other.ToString()
                                      && e.GetProperty("action").GetString() == "order.cancel"
                                      && e.GetProperty("outcome").GetString() == "failed"
                                      && e.GetProperty("actorId").GetString() == attackerId.ToString());

        // The refused attempt did not cancel the order.
        var json = await Client.GetFromJsonAsync<JsonElement>($"/orders/{other}");
        json.GetProperty("status").GetString().Should().NotBe("Cancelled");
    }

    private static (HttpStatusCode Status, string Code) ExpectedRefusal(Caller caller) => caller switch
    {
        Caller.Anonymous or Caller.InvalidSignature or Caller.Expired => (HttpStatusCode.Unauthorized, "unauthenticated"),
        _ => (HttpStatusCode.Forbidden, "forbidden")
    };

    private async Task<List<JsonElement>> AuditItems()
    {
        var audit = await Client.GetFromJsonAsync<JsonElement>("/orders/admin/audit?pageSize=100");
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
            Caller.Customer => client.AsCustomer(Guid.NewGuid()),
            Caller.LegacyManager => client.WithBearer(TestTokens.Create("Manager", Guid.NewGuid())),
            Caller.LegacySupport => client.WithBearer(TestTokens.Create("Support", Guid.NewGuid())),
            _ => throw new ArgumentOutOfRangeException(nameof(caller))
        };
    }

    private static HttpRequestMessage BuildRequest(string method, string template, Guid orderId, Guid ownerId, string name)
    {
        var path = template.Replace("{o}", orderId.ToString()).Replace("{u}", ownerId.ToString());
        var request = new HttpRequestMessage(new HttpMethod(method), path);

        switch (name)
        {
            case "cancel-order":
                request.Content = JsonContent.Create(new { reason = "access matrix probe" });
                break;
            case "update-address":
                request.Content = JsonContent.Create(new
                {
                    street = "1 Matrix Way",
                    city = "Bucharest",
                    state = "B",
                    country = "RO",
                    zipCode = "010101"
                });
                break;
        }

        return request;
    }

    private async Task<Guid> CreateOrderAsync(Guid ownerId)
    {
        var response = await Client.PostAsJsonAsync("/orders", new
        {
            UserId = ownerId,
            Currency = "EUR",
            ShippingAddress = new { Street = "Test St", City = "City", State = "State", Country = "Country", ZipCode = "00000" },
            Items = new[]
            {
                new { ProductId = Guid.NewGuid(), ProductName = "Widget", UnitPrice = 15m, Quantity = 1, Currency = "EUR", ProductImageUrl = (string?)null }
            }
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderResponse>())!.OrderId;
    }

    private async Task<string> GetOrderJson(Guid orderId)
    {
        using var response = await Client.GetAsync($"/orders/{orderId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}