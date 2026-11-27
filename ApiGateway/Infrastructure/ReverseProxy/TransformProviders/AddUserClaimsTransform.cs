using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Infrastructure.ReverseProxy.TransformProviders;

/// <summary>
/// Forwards the caller's identity to downstream services from the verified token only.
/// Any identity header sent by the client is removed first so it can never be trusted (FR-005).
/// </summary>
public class AddUserClaimsTransform : ITransformProvider
{
    public const string UserIdHeader = "X-User-Id";
    public const string UserEmailHeader = "X-User-Email";
    public const string UserRoleHeader = "X-User-Role";

    private static readonly string[] IdentityHeaders = [UserIdHeader, UserEmailHeader, UserRoleHeader];

    public void Apply(TransformBuilderContext context)
    {
        context.AddRequestTransform(ctx =>
        {
            foreach (var header in IdentityHeaders)
            {
                ctx.ProxyRequest.Headers.Remove(header);
            }

            var user = ctx.HttpContext.User;

            if (user.Identity?.IsAuthenticated == true)
            {
                var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

                if (!string.IsNullOrEmpty(userId))
                {
                    ctx.ProxyRequest.Headers.TryAddWithoutValidation(UserIdHeader, userId);
                }

                var email = user.FindFirst(ClaimTypes.Email)?.Value
                    ?? user.FindFirst(JwtRegisteredClaimNames.Email)?.Value;

                if (!string.IsNullOrEmpty(email))
                {
                    ctx.ProxyRequest.Headers.TryAddWithoutValidation(UserEmailHeader, email);
                }
            }

            return ValueTask.CompletedTask;
        });
    }

    public void ValidateRoute(TransformRouteValidationContext context) { }

    public void ValidateCluster(TransformClusterValidationContext context) { }
}