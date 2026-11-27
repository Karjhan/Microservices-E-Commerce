using Application.Features.Products.ReorderImages;
using MediatR;
using Shared.Security.Authorization;

namespace Catalog.Endpoints.Products;

public static class ReorderImagesEndpoint
{
    public static IEndpointRouteBuilder MapReorderImages(this IEndpointRouteBuilder app)
    {
        app.MapPut("/{id:guid}/images/order", async (
                Guid id,
                ReorderImagesRequest request,
                IMediator mediator,
                CancellationToken ct) =>
            {
                var images = await mediator.Send(new ReorderImagesCommand(id, request.ImageIds ?? []), ct);
                return Results.Ok(images);
            })
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }
}

public record ReorderImagesRequest(IReadOnlyList<Guid>? ImageIds);
