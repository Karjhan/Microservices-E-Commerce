using Application.DTOs;
using MediatR;

namespace Application.Features.Inventory.Adjust;

public record AdjustInventoryCommand(
    Guid ProductId,
    int Delta,
    string Reason
) : IRequest<InventoryItemDto>;
