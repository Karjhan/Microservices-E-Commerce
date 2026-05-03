using Application.Features.Products.GetRelations;
using MediatR;

namespace Catalog.Endpoints.Products;

public static class GetProductRelationsEndpoint
{
    public static IEndpointRouteBuilder MapGetProductRelations(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{productId:guid}/relations", async (
            Guid productId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetProductRelationsQuery(productId), ct);
            return Results.Ok(result);
        });

        return app;
    }
}