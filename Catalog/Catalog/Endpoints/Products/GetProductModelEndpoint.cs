using Application.Features.Products.GetModel;
using MediatR;

namespace Catalog.Endpoints.Products;

public static class GetProductModelEndpoint
{
    public static IEndpointRouteBuilder MapGetProductModel(this IEndpointRouteBuilder app)
    {
        // Public for Active products; the handler hides everything else from non-admins.
        app.MapGet("/{id:guid}/model", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var model = await mediator.Send(new GetProductModelQuery(id), ct);
            return Results.Ok(model);
        });

        return app;
    }
}
