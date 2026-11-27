using Application.Features.Inventory.Movements;
using MediatR;
using Shared.Security.Authorization;

namespace Inventory.Endpoints;

public static class GetInventoryMovementsEndpoint
{
    public static IEndpointRouteBuilder MapGetInventoryMovements(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{productId:guid}/movements", async (
            Guid productId,
            int? page,
            int? pageSize,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(
                new GetInventoryMovementsQuery(productId, page ?? 1, pageSize ?? 20), ct);
            return Results.Ok(result);
        }).RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }
}
