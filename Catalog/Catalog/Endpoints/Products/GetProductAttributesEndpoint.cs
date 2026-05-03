using Application.Features.Products.GetAttributes;
using MediatR;

namespace Catalog.Endpoints.Products;

public static class GetProductAttributesEndpoint
{
    public static IEndpointRouteBuilder MapGetProductAttributes(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{productId:guid}/attributes", async (
            Guid productId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetProductAttributesQuery(productId), ct);
            return Results.Ok(result);
        });

        return app;
    }
}