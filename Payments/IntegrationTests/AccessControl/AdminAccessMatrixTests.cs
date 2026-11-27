using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using IntegrationTests.Helpers;
using Shared.Security.TestSupport;

namespace IntegrationTests.AccessControl;

/// <summary>
/// SC-001: Payments refuses every caller that is not allowed — refunds and listings are Admin only,
/// owner routes are owner-or-Admin and a non-owner gets 404 so existence is not revealed.
/// </summary>
[Trait("Category", "AccessControl")]
public class AdminAccessMatrixTests : IntegrationTestBase
{
    public AdminAccessMatrixTests(PaymentsApiFactory factory) : base(factory) { }

    public enum Caller
    {
        Anonymous,
        InvalidSignature,
        Expired,
        Customer,
        LegacyManager,
        LegacySupport
    }

    // Admin-only: Name, method, path template ({p} payment id)
    public static IEnumerable<object[]> AdminRoutes() =>
    [
        ["list-payments", "GET", "/payments?page=1&pageSize=10"],
        ["refund-payment", "POST", "/payments/{p}/refund"],
        ["audit", "GET", "/payments/admin/audit"]
    ];

    // Owner-or-admin: Name, method, path template ({p} payment id, {o} order id)
    public static IEnumerable<object[]> OwnerRoutes() =>
    [
        ["get-payment", "GET", "/payments/{p}"],
        ["get-by-order", "GET", "/payments/by-order/{o}"],
        ["process-payment", "POST", "/payments/{p}/process"]
    ];

    public static IEnumerable<object[]> AllCallers() =>
        Enum.GetValues<Caller>().Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(AllCallers))]
    public async Task Admin_routes_refuse_non_admin_callers_and_change_nothing(Caller caller)
    {
        var (paymentId, orderId, _) = await CreatePaymentAsync(Guid.NewGuid());
        var before = await GetPaymentJson(paymentId);

        var (expectedStatus, expectedCode) = caller switch
        {
            Caller.Anonymous or Caller.InvalidSignature or Caller.Expired => (HttpStatusCode.Unauthorized, "unauthenticated"),
            _ => (HttpStatusCode.Forbidden, "forbidden")
        };

        foreach (var route in AdminRoutes())
        {
            var (name, method, template) = ((string)route[0], (string)route[1], (string)route[2]);

            using var client = CreateClient(caller);
            using var request = BuildRequest(method, template, paymentId, orderId);
            using var response = await client.SendAsync(request);

            response.StatusCode.Should().Be(expectedStatus, $"{name} as {caller}");

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("code").GetString().Should().Be(expectedCode, $"{name} as {caller}");

            if (expectedStatus == HttpStatusCode.Unauthorized)
            {
                response.Headers.WwwAuthenticate.Should().Contain(h => h.Scheme == "Bearer", $"{name} as {caller}");
            }
        }

        (await GetPaymentJson(paymentId)).Should().Be(before, "refused calls must not change the payment");
    }

    [Theory]
    [MemberData(nameof(AllCallers))]
    public async Task Owner_routes_refuse_everyone_but_the_owner_and_admins_and_change_nothing(Caller caller)
    {
        var (paymentId, orderId, _) = await CreatePaymentAsync(Guid.NewGuid());
        var before = await GetPaymentJson(paymentId);

        // Signed-out callers get 401; signed-in non-owners get 404 (never 403) so existence is not revealed.
        var expectedStatus = caller is Caller.Anonymous or Caller.InvalidSignature or Caller.Expired
            ? HttpStatusCode.Unauthorized
            : HttpStatusCode.NotFound;

        foreach (var route in OwnerRoutes())
        {
            var (name, method, template) = ((string)route[0], (string)route[1], (string)route[2]);

            using var client = CreateClient(caller);
            using var request = BuildRequest(method, template, paymentId, orderId);
            using var response = await client.SendAsync(request);

            response.StatusCode.Should().Be(expectedStatus, $"{name} as {caller}");
        }

        (await GetPaymentJson(paymentId)).Should().Be(before, "refused calls must not change the payment");
    }

    [Fact]
    public async Task Owner_can_read_their_own_payment()
    {
        var ownerId = Guid.NewGuid();
        var (paymentId, orderId, _) = await CreatePaymentAsync(ownerId);
        using var owner = Factory.CreateClient().AsCustomer(ownerId);

        (await owner.GetAsync($"/payments/{paymentId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.GetAsync($"/payments/by-order/{orderId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_is_allowed_on_every_read_and_admin_route()
    {
        var (paymentId, orderId, _) = await CreatePaymentAsync(Guid.NewGuid());

        foreach (var route in AdminRoutes().Concat(OwnerRoutes()))
        {
            var (name, method, template) = ((string)route[0], (string)route[1], (string)route[2]);
            if (name == "process-payment")
            {
                continue; // would charge through the payment provider
            }

            using var request = BuildRequest(method, template, paymentId, orderId);
            using var response = await Client.SendAsync(request);

            // A refund of a payment that was never charged is a business refusal, not an access refusal.
            response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized, name);
            response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden, name);

            if (name != "refund-payment")
            {
                response.StatusCode.Should().Be(HttpStatusCode.OK, name);
            }
        }
    }

    [Fact]
    public async Task Creating_a_payment_requires_sign_in_and_the_callers_own_user_id()
    {
        var customerId = Guid.NewGuid();
        using var anonymous = Factory.CreateClient();
        using var customer = Factory.CreateClient().AsCustomer(customerId);

        (await anonymous.PostAsJsonAsync("/payments", PaymentBody(customerId)))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var other = await customer.PostAsJsonAsync("/payments", PaymentBody(Guid.NewGuid()));
        other.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var own = await customer.PostAsJsonAsync("/payments", PaymentBody(customerId));
        own.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Refusals_are_recorded_in_the_audit_trail_with_the_actor_when_known()
    {
        var customerId = Guid.NewGuid();
        using var customer = Factory.CreateClient().AsCustomer(customerId);
        using var anonymous = Factory.CreateClient();

        var path = $"/payments/{Guid.NewGuid()}/refund";
        (await customer.PostAsync(path, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await anonymous.PostAsync(path, null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

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
    public async Task Failed_refunds_are_recorded_and_change_nothing()
    {
        // A payment that was never charged cannot be refunded.
        var (paymentId, _, _) = await CreatePaymentAsync(Guid.NewGuid());
        var before = await GetPaymentJson(paymentId);

        var response = await Client.PostAsJsonAsync($"/payments/{paymentId}/refund", new { amount = 5m });
        response.IsSuccessStatusCode.Should().BeFalse();

        (await GetPaymentJson(paymentId)).Should().Be(before);

        (await AuditItems()).Should().Contain(e =>
            e.GetProperty("targetId").GetString() == paymentId.ToString()
            && e.GetProperty("action").GetString() == "payment.refund"
            && e.GetProperty("outcome").GetString() == "failed");
    }

    private async Task<List<JsonElement>> AuditItems()
    {
        var audit = await Client.GetFromJsonAsync<JsonElement>("/payments/admin/audit?pageSize=100");
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

    private static HttpRequestMessage BuildRequest(string method, string template, Guid paymentId, Guid orderId)
    {
        var path = template.Replace("{p}", paymentId.ToString()).Replace("{o}", orderId.ToString());
        var request = new HttpRequestMessage(new HttpMethod(method), path);

        if (path.EndsWith("/refund"))
        {
            request.Content = JsonContent.Create(new { amount = 1m });
        }

        return request;
    }

    private static object PaymentBody(Guid userId, Guid? orderId = null) => new
    {
        OrderId = orderId ?? Guid.NewGuid(),
        UserId = userId,
        Amount = 25m,
        Currency = "EUR",
        Method = 0,
        IdempotencyKey = Guid.NewGuid().ToString(),
        PaymentToken = "pm_test_token",
        RecipientEmail = "matrix@example.com"
    };

    private async Task<(Guid PaymentId, Guid OrderId, Guid UserId)> CreatePaymentAsync(Guid userId)
    {
        var orderId = Guid.NewGuid();
        var response = await Client.PostAsJsonAsync("/payments", PaymentBody(userId, orderId));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<PaymentResponse>();
        return (body!.PaymentId, orderId, userId);
    }

    private async Task<string> GetPaymentJson(Guid paymentId)
    {
        using var response = await Client.GetAsync($"/payments/{paymentId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}