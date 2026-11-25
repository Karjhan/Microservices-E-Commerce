using Application.Abstractions.Persistence;
using Application.Features.Inventory.List;
using MediatR;
using Shared.Security.Authorization;

namespace Inventory.Endpoints;

public static class ListInventoryEndpoint
{
    public static IEndpointRouteBuilder MapListInventory(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", async (
            bool? depletedOnly,
            int? page,
            int? pageSize,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var filter = new InventoryFilter
            {
                DepletedOnly = depletedOnly,
                Page = page is null or <= 0 ? 1 : page.Value,
                PageSize = pageSize is null or <= 0 ? 20 : Math.Min(pageSize.Value, 100)
            };

            var result = await mediator.Send(new ListInventoryQuery(filter), ct);
            return Results.Ok(result);
        }).RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }
}
