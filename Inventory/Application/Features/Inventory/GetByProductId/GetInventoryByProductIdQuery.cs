using Application.DTOs;
using MediatR;

namespace Application.Features.Inventory.GetByProductId;

public record GetInventoryByProductIdQuery(Guid ProductId) : IRequest<InventoryLookupResult>;

/// <summary>
/// Admins get the full record (<see cref="Full"/>); everyone else gets only availability (<see cref="Public"/>).
/// <see cref="Body"/> is the one to send back to the caller.
/// </summary>
public record InventoryLookupResult(PublicInventoryDto Public, InventoryItemDto? Full = null)
{
    public object Body => Full is not null ? Full : Public;
}
