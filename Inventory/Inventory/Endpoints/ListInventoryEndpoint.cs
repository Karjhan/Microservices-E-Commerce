using Application.Abstractions.Persistence;
using Application.Features.Inventory.List;
using Domain.Enums;
using Domain.Exceptions;
using MediatR;
using Shared.Security.Authorization;

namespace Inventory.Endpoints;

public static class ListInventoryEndpoint
{
    private const int MaxProductIds = 100;

    public static IEndpointRouteBuilder MapListInventory(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", async (
            string? status,
            string? productIds,
            bool? depletedOnly,
            int? page,
            int? pageSize,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var filter = new InventoryFilter
            {
                Status = ParseStatus(status, depletedOnly),
                ProductIds = ParseProductIds(productIds),
                Page = page is null or <= 0 ? 1 : page.Value,
                PageSize = pageSize is null or <= 0 ? 20 : Math.Min(pageSize.Value, 100)
            };

            var result = await mediator.Send(new ListInventoryQuery(filter), ct);
            return Results.Ok(result);
        }).RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }

    /// <summary><c>all</c> (or nothing), <c>low</c> or <c>out</c>. The older <c>depletedOnly</c> means <c>out</c>.</summary>
    private static StockStatus? ParseStatus(string? status, bool? depletedOnly)
    {
        if (string.IsNullOrWhiteSpace(status))
            return depletedOnly == true ? StockStatus.Out : null;

        return status.Trim().ToLowerInvariant() switch
        {
            "all" => null,
            "low" => StockStatus.Low,
            "out" => StockStatus.Out,
            _ => throw new InventoryValidationException("status", "Status must be \"all\", \"low\" or \"out\".")
        };
    }

    /// <summary>A comma separated list of product ids. Names the products the list is limited to.</summary>
    private static IReadOnlyList<Guid>? ParseProductIds(string? productIds)
    {
        if (string.IsNullOrWhiteSpace(productIds))
            return null;

        var ids = new List<Guid>();
        foreach (var part in productIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Guid.TryParse(part, out var id))
                throw new InventoryValidationException("productIds", $"\"{part}\" is not a product id.");
            ids.Add(id);
        }

        if (ids.Count > MaxProductIds)
            throw new InventoryValidationException("productIds", $"At most {MaxProductIds} product ids at a time.");

        return ids.Distinct().ToList();
    }
}
