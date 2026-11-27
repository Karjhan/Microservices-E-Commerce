using Application.Features.Inventory.Adjust;
using Domain.Exceptions;
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
            if (!Enum.TryParse<AdjustMode>(request.Mode, ignoreCase: true, out var mode)
                || !Enum.IsDefined(mode))
                throw new InventoryValidationException("mode", "Mode must be \"delta\" or \"set\".");

            var command = new AdjustInventoryCommand(
                productId, mode, request.Quantity, request.Reason ?? string.Empty, request.ExpectedVersion);
            var result = await mediator.Send(command, ct);
            return Results.Ok(result);
        }).RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }
}

/// <param name="Mode"><c>delta</c> adds or removes <paramref name="Quantity"/>; <c>set</c> makes it the new count.</param>
/// <param name="ExpectedVersion">Optional: the <c>version</c> the admin was looking at (409 <c>stale_version</c> when stale).</param>
public record AdjustInventoryRequest(string? Mode, int Quantity, string? Reason, string? ExpectedVersion = null);
