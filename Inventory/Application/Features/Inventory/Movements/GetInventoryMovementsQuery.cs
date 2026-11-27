using Application.DTOs;
using MediatR;

namespace Application.Features.Inventory.Movements;

public record GetInventoryMovementsQuery(Guid ProductId, int Page = 1, int PageSize = 20)
    : IRequest<InventoryMovementListDto>;
