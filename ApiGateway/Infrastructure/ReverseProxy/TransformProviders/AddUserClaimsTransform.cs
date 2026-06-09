using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Infrastructure.ReverseProxy.TransformProviders;

public class AddUserClaimsTransform : ITransformProvider
{
    public void Apply(TransformBuilderContext context)
    {
        context.AddRequestTransform(ctx =>
        {
            var user = ctx.HttpContext.User;

            if (user.Identity?.IsAuthenticated == true)
            {
                var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

                if (!string.IsNullOrEmpty(userId))
                {
                    ctx.ProxyRequest.Headers.TryAddWithoutValidation("X-User-Id", userId);
                }

                var email = user.FindFirst(ClaimTypes.Email)?.Value
                    ?? user.FindFirst(JwtRegisteredClaimNames.Email)?.Value;

                if (!string.IsNullOrEmpty(email))
                {
                    ctx.ProxyRequest.Headers.TryAddWithoutValidation("X-User-Email", email);
                }
            }

            return ValueTask.CompletedTask;
        });
    }

    public void ValidateRoute(TransformRouteValidationContext context) { }

    public void ValidateCluster(TransformClusterValidationContext context) { }
}