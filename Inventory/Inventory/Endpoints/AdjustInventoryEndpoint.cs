using Application.Features.Inventory.Adjust;
using MediatR;
using Shared.Security.Authorization;

namespace Inventory.Endpoints;

public static class AdjustInventoryEndpoint
{
    public static IEndpointRouteBuilder MapAdjustInventory(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{productId:guid}/adjustments", async (
            Guid productId,
            AdjustInventoryRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new AdjustInventoryCommand(productId, request.Delta, request.Reason);
            var result = await mediator.Send(command, ct);
            return Results.Ok(result);
        }).RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }
}

public record AdjustInventoryRequest(int Delta, string Reason);
