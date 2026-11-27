using Application.Features.Inventory.GetByProductId;
using MediatR;

namespace Inventory.Endpoints;

public static class GetInventoryByProductIdEndpoint
{
    public static IEndpointRouteBuilder MapGetInventoryByProductId(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{productId:guid}", async (
            Guid productId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetInventoryByProductIdQuery(productId), ct);
            return Results.Ok(result.Body);
        });

        return app;
    }
}
