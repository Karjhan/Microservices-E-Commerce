using Application.DTOs;
using MediatR;

namespace Application.Features.Inventory.GetByProductId;

public record GetInventoryByProductIdQuery(Guid ProductId) : IRequest<InventoryItemDto>;
