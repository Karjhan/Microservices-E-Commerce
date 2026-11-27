using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Security.TestSupport;

namespace IntegrationTests.Tests.AccessControl;

/// <summary>
/// The gateway refuses administrative routes early (FR-001) and forwards identity only from the verified
/// token (FR-005). Downstream services are replaced by a tiny echo server, so no containers are needed.
/// </summary>
[Trait("Category", "AccessControl")]
public class GatewayAccessTests : IAsyncLifetime
{
    private WebApplication _echo = null!;
    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        _echo = builder.Build();
        _echo.Map("/{**path}", (HttpContext context) => Results.Json(new
        {
            method = context.Request.Method,
            path = context.Request.Path.Value,
            userId = context.Request.Headers["X-User-Id"].ToString(),
            userEmail = context.Request.Headers["X-User-Email"].ToString(),
            userRole = context.Request.Headers["X-User-Role"].ToString()
        }));
        await _echo.StartAsync();

        var echoUrl = _echo.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Testing");
            host.ConfigureAppConfiguration((_, cfg) =>
            {
                var clusters = new[] { "auth", "catalog", "payments", "orders", "inventory" };
                cfg.AddInMemoryCollection(clusters.ToDictionary(
                    c => $"ReverseProxy:Clusters:{c}-cluster:Destinations:{c}:Address",
                    c => (string?)echoUrl));
            });
        });
    }

    public async Task DisposeAsync()
    {
        _factory.Dispose();
        await _echo.StopAsync();
        await _echo.DisposeAsync();
    }

    public static IEnumerable<object[]> AdminRequests() =>
    [
        ["GET", "/orders"],
        ["GET", "/orders/admin/audit"],
        ["GET", "/payments"],
        ["POST", "/payments/11111111-1111-1111-1111-111111111111/refund"],
        ["GET", "/payments/admin/audit"],
        ["GET", "/inventory"],
        ["POST", "/inventory/11111111-1111-1111-1111-111111111111/adjustments"],
        ["GET", "/inventory/admin/audit"],
        ["POST", "/products"],
        ["PUT", "/products/11111111-1111-1111-1111-111111111111"],
        ["DELETE", "/products/11111111-1111-1111-1111-111111111111"],
        ["POST", "/products/11111111-1111-1111-1111-111111111111/images"],
        ["DELETE", "/products/11111111-1111-1111-1111-111111111111/images/22222222-2222-2222-2222-222222222222"],
        ["POST", "/products/11111111-1111-1111-1111-111111111111/attributes"],
        ["DELETE", "/products/11111111-1111-1111-1111-111111111111/attributes/22222222-2222-2222-2222-222222222222"],
        ["POST", "/products/11111111-1111-1111-1111-111111111111/relations"],
        ["DELETE", "/products/11111111-1111-1111-1111-111111111111/relations/22222222-2222-2222-2222-222222222222"],
        ["GET", "/products/admin"],
        ["POST", "/products/11111111-1111-1111-1111-111111111111/status"],
        ["POST", "/products/11111111-1111-1111-1111-111111111111/images/22222222-2222-2222-2222-222222222222/primary"],
        ["PUT", "/products/11111111-1111-1111-1111-111111111111/images/order"],
        ["PUT", "/products/11111111-1111-1111-1111-111111111111/model"],
        ["DELETE", "/products/11111111-1111-1111-1111-111111111111/model"],
        ["GET", "/products/admin/audit"],
        ["GET", "/auth/admin/audit"]
    ];

    public static IEnumerable<object[]> PublicRequests() =>
    [
        ["GET", "/products"],
        ["GET", "/products/11111111-1111-1111-1111-111111111111"],
        ["GET", "/products/11111111-1111-1111-1111-111111111111/model"],
        ["GET", "/inventory/11111111-1111-1111-1111-111111111111"],
        ["POST", "/orders"],
        ["POST", "/auth/login"]
    ];

    [Theory]
    [MemberData(nameof(AdminRequests))]
    public async Task Admin_routes_refuse_signed_out_callers_with_401(string method, string path)
    {
        using var client = _factory.CreateClient();

        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()
            .Should().Be("unauthenticated");
    }

    [Theory]
    [MemberData(nameof(AdminRequests))]
    public async Task Admin_routes_refuse_customers_and_legacy_roles_with_403(string method, string path)
    {
        foreach (var token in new[]
                 {
                     TestTokens.Customer(),
                     TestTokens.Create("Manager"),
                     TestTokens.Create("Support")
                 })
        {
            using var client = _factory.CreateClient().WithBearer(token);

            using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()
                .Should().Be("forbidden");
        }
    }

    [Theory]
    [MemberData(nameof(AdminRequests))]
    public async Task Admin_routes_reach_the_service_for_admins(string method, string path)
    {
        using var client = _factory.CreateClient().AsAdmin();

        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("path").GetString().Should().Be(path);
    }

    [Theory]
    [MemberData(nameof(PublicRequests))]
    public async Task Public_routes_stay_open_to_signed_out_callers(string method, string path)
    {
        using var client = _factory.CreateClient();

        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Identity_headers_sent_by_the_client_are_never_forwarded()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/products");
        request.Headers.Add("X-User-Id", Guid.NewGuid().ToString());
        request.Headers.Add("X-User-Email", "attacker@evil.test");
        request.Headers.Add("X-User-Role", "Admin");

        using var response = await client.SendAsync(request);
        var echoed = await response.Content.ReadFromJsonAsync<JsonElement>();

        echoed.GetProperty("userId").GetString().Should().BeEmpty();
        echoed.GetProperty("userEmail").GetString().Should().BeEmpty();
        echoed.GetProperty("userRole").GetString().Should().BeEmpty();
    }

    [Fact]
    public async Task Identity_headers_come_from_the_verified_token_not_the_client()
    {
        var realId = Guid.NewGuid();
        using var client = _factory.CreateClient().WithBearer(TestTokens.Customer(realId, "real@store.test"));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/products");
        request.Headers.Add("X-User-Id", Guid.NewGuid().ToString());
        request.Headers.Add("X-User-Email", "attacker@evil.test");
        request.Headers.Add("X-User-Role", "Admin");

        using var response = await client.SendAsync(request);
        var echoed = await response.Content.ReadFromJsonAsync<JsonElement>();

        echoed.GetProperty("userId").GetString().Should().Be(realId.ToString());
        echoed.GetProperty("userEmail").GetString().Should().Be("real@store.test");
        echoed.GetProperty("userRole").GetString().Should().BeEmpty();
    }
}