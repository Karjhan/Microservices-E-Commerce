using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Infrastructure.ReverseProxy.TransformProviders;

public class AddCorrelationIdTransform : ITransformProvider
{
    public void Apply(TransformBuilderContext context)
    {
        context.AddRequestTransform(ctx =>
        {
            if (ctx.HttpContext.Items.TryGetValue("X-Correlation-Id", out var id))
            {
                ctx.ProxyRequest.Headers.TryAddWithoutValidation("X-Correlation-Id", id!.ToString());
            }

            return ValueTask.CompletedTask;
        });
    }

    public void ValidateRoute(TransformRouteValidationContext context) { }

    public void ValidateCluster(TransformClusterValidationContext context) { }
}