using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Shared.Security.TestSupport;

/// <summary>
/// Mints access tokens for integration tests with the same claim shape the Auth service issues
/// (<c>sub</c>, <c>email</c>, role) and the development signing settings from each service's appsettings.json.
/// </summary>
public static class TestTokens
{
    public const string Key = "super-secret-key-that-never-be-found-out-in-this-life";
    public const string Issuer = "auth-service";
    public const string Audience = "ecommerce";

    public static readonly Guid DefaultAdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DefaultCustomerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>Creates a token with exactly the given role string (use "Manager" or "Support" to simulate legacy tokens).</summary>
    public static string Create(
        string role,
        Guid? userId = null,
        string? email = null,
        TimeSpan? lifetime = null,
        string? key = null)
    {
        var id = userId ?? (role == "Admin" ? DefaultAdminId : DefaultCustomerId);
        var mail = email ?? $"{role.ToLowerInvariant()}@test.local";

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key ?? Key)),
            SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, mail),
            new Claim(ClaimTypes.Role, role)
        };

        var now = DateTime.UtcNow;
        var expires = now.Add(lifetime ?? TimeSpan.FromMinutes(15));
        // notBefore must not be after expires, which matters for already-expired tokens.
        var notBefore = expires < now ? expires.AddMinutes(-1) : now.AddMinutes(-1);

        var token = new JwtSecurityToken(Issuer, Audience, claims, notBefore, expires, credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string Admin(Guid? userId = null, string? email = null) => Create("Admin", userId, email);

    public static string Customer(Guid? userId = null, string? email = null) => Create("Customer", userId, email);

    public static string Expired(string role = "Admin") => Create(role, lifetime: TimeSpan.FromMinutes(-10));

    public static string WrongSignature(string role = "Admin") =>
        Create(role, key: "another-secret-key-that-is-long-enough-for-hmac-sha256");

    public static HttpClient AsAdmin(this HttpClient client, Guid? userId = null)
        => client.WithBearer(Admin(userId));

    public static HttpClient AsCustomer(this HttpClient client, Guid? userId = null)
        => client.WithBearer(Customer(userId));

    public static HttpClient WithBearer(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static HttpClient Anonymous(this HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = null;
        return client;
    }
}