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
                var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (!string.IsNullOrEmpty(userId))
                {
                    ctx.ProxyRequest.Headers.TryAddWithoutValidation("X-User-Id", userId);
                }
            }

            return ValueTask.CompletedTask;
        });
    }

    public void ValidateRoute(TransformRouteValidationContext context) { }

    public void ValidateCluster(TransformClusterValidationContext context) { }
}