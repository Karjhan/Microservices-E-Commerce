using Application.Features.Inventory.SetThreshold;
using MediatR;
using Shared.Security.Authorization;

namespace Inventory.Endpoints;

public static class SetLowStockThresholdEndpoint
{
    public static IEndpointRouteBuilder MapSetLowStockThreshold(this IEndpointRouteBuilder app)
    {
        app.MapPut("/{productId:guid}/threshold", async (
            Guid productId,
            SetLowStockThresholdRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new SetLowStockThresholdCommand(productId, request.LowStockThreshold), ct);
            return Results.Ok(result);
        }).RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }
}

public record SetLowStockThresholdRequest(int LowStockThreshold);
