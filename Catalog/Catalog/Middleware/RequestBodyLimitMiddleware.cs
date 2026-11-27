using Microsoft.AspNetCore.Http.Features;

namespace Catalog.Middleware;

/// <summary>Marks an endpoint as allowed to receive a larger request body than the server default.</summary>
public sealed record MaxRequestBodySizeMetadata(long Bytes);

/// <summary>
/// Applies <see cref="MaxRequestBodySizeMetadata"/> before the body is read. Only endpoints that carry the metadata
/// are affected, so every other route keeps the default limit (research R8).
/// </summary>
public class RequestBodyLimitMiddleware(RequestDelegate next)
{
    public Task Invoke(HttpContext context)
    {
        var metadata = context.GetEndpoint()?.Metadata.GetMetadata<MaxRequestBodySizeMetadata>();

        if (metadata is not null)
        {
            var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();

            if (feature is { IsReadOnly: false })
                feature.MaxRequestBodySize = metadata.Bytes;
        }

        return next(context);
    }
}
