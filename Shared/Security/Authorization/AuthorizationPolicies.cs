using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Shared.Security.Authorization;

public static class AuthorizationPolicies
{
    public const string AdminOnly = "AdminOnly";

    public static AuthorizationOptions AddAdminOnlyPolicy(this AuthorizationOptions options)
    {
        options.AddPolicy(AdminOnly, policy => policy
            .RequireAuthenticatedUser()
            .RequireClaim(ClaimTypes.Role, Roles.Admin));

        return options;
    }
}
