using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Shared.Security.Authorization;

public interface ICurrentUser
{
    Guid? Id { get; }
    string? Email { get; }
    string Role { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }
    string? CorrelationId { get; }
}

public sealed class CurrentUser : ICurrentUser
{
    private readonly HttpContext? _context;

    private ClaimsPrincipal? Principal => _context?.User;

    public CurrentUser(IHttpContextAccessor accessor)
    {
        _context = accessor.HttpContext;
    }

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? Id
    {
        get
        {
            var raw = Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? Email =>
        Principal?.FindFirstValue(ClaimTypes.Email)
        ?? Principal?.FindFirstValue(JwtRegisteredClaimNames.Email);

    public string Role =>
        IsAuthenticated
            ? Roles.NormalizeRole(Principal?.FindFirstValue(ClaimTypes.Role))
            : string.Empty;

    public bool IsAdmin => IsAuthenticated && Role == Roles.Admin;

    public string? CorrelationId =>
        _context?.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? _context?.TraceIdentifier;
}
