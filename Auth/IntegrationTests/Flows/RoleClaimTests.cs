using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using FluentAssertions;
using Infrastructure.Persistence;
using IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Flows;

/// <summary>FR-003: tokens carry only Admin or Customer, at login and at refresh.</summary>
[Collection("Integration")]
public class RoleClaimTests : IClassFixture<AuthApiFactory>
{
    private const string Password = "password";

    private readonly HttpClient _client;
    private readonly IServiceScope _scope;
    private readonly AuthDbContext _db;

    public RoleClaimTests(AuthApiFactory factory)
    {
        _client = factory.CreateClient();
        _scope = factory.Services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        _db.Database.EnsureDeleted();
        _db.Database.Migrate();
    }

    private async Task RegisterWithStoredRole(string email, string storedRole)
    {
        var register = await _client.PostAsJsonAsync("/auth/register", new { email, password = Password });
        register.IsSuccessStatusCode.Should().BeTrue();

        // The role check constraint (correctly) refuses legacy roles. Drop it for this test only so
        // we can simulate a database that still holds a pre-migration Manager or Support row.
        await _db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Users\" DROP CONSTRAINT IF EXISTS \"CK_Users_Role\"");
        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE \"Users\" SET \"Role\" = {0} WHERE \"NormalizedEmail\" = {1}", storedRole, email);
    }

    private async Task<AuthResponse> Login(string email)
    {
        var response = await _client.PostAsJsonAsync("/auth/login", new { email, password = Password });
        response.IsSuccessStatusCode.Should().BeTrue();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private static string RoleOf(string accessToken)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        return jwt.Claims.Single(c => c.Type is "role" or "http://schemas.microsoft.com/ws/2008/06/identity/claims/role").Value;
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("Support")]
    [InlineData("Customer")]
    public async Task Non_admin_stored_roles_receive_customer_on_login_and_refresh(string storedRole)
    {
        var email = $"{storedRole.ToLowerInvariant()}@test.com";
        await RegisterWithStoredRole(email, storedRole);

        var tokens = await Login(email);
        RoleOf(tokens.AccessToken).Should().Be("Customer");

        var refreshed = await _client.PostAsJsonAsync("/auth/refresh", new { refreshToken = tokens.RefreshToken });
        refreshed.IsSuccessStatusCode.Should().BeTrue();
        var refreshedTokens = (await refreshed.Content.ReadFromJsonAsync<AuthResponse>())!;
        RoleOf(refreshedTokens.AccessToken).Should().Be("Customer");
    }

    [Fact]
    public async Task Admin_keeps_admin_on_login_and_refresh()
    {
        var email = "admin@test.com";
        await RegisterWithStoredRole(email, "Admin");

        var tokens = await Login(email);
        RoleOf(tokens.AccessToken).Should().Be("Admin");

        var refreshed = await _client.PostAsJsonAsync("/auth/refresh", new { refreshToken = tokens.RefreshToken });
        refreshed.IsSuccessStatusCode.Should().BeTrue();
        var refreshedTokens = (await refreshed.Content.ReadFromJsonAsync<AuthResponse>())!;
        RoleOf(refreshedTokens.AccessToken).Should().Be("Admin");
    }

    [Fact]
    public async Task Demotion_applies_at_the_next_refresh()
    {
        var email = "demoted@test.com";
        await RegisterWithStoredRole(email, "Admin");
        var tokens = await Login(email);

        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE \"Users\" SET \"Role\" = 'Customer' WHERE \"NormalizedEmail\" = {0}", email);

        var refreshed = await _client.PostAsJsonAsync("/auth/refresh", new { refreshToken = tokens.RefreshToken });
        var refreshedTokens = (await refreshed.Content.ReadFromJsonAsync<AuthResponse>())!;

        RoleOf(refreshedTokens.AccessToken).Should().Be("Customer");
    }

    [Fact]
    public async Task Access_token_lifetime_is_at_most_15_minutes()
    {
        var email = "lifetime@test.com";
        await RegisterWithStoredRole(email, "Customer");

        var tokens = await Login(email);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokens.AccessToken);
        (jwt.ValidTo - DateTime.UtcNow).Should().BeLessThanOrEqualTo(TimeSpan.FromMinutes(15.5));
    }
}
