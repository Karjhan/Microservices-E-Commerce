using Application.Abstractions.Persistence;
using Application.DTOs;
using Domain.Exceptions;
using MediatR;

namespace Application.Features.Inventory.Movements;

public class GetInventoryMovementsHandler(IInventoryRepository repo)
    : IRequestHandler<GetInventoryMovementsQuery, InventoryMovementListDto>
{
    public const int MaxPageSize = 100;

    public async Task<InventoryMovementListDto> Handle(GetInventoryMovementsQuery request, CancellationToken ct)
    {
        // A product that never had stock has no history to show.
        _ = await repo.GetByProductIdAsync(request.ProductId, ct)
            ?? throw new InventoryItemNotFoundException(request.ProductId);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        var movements = await repo.GetMovementsAsync(request.ProductId, page, pageSize, ct);
        var total = await repo.CountMovementsAsync(request.ProductId, ct);

        return new InventoryMovementListDto
        {
            Items = movements.Select(InventoryMovementDto.FromEntity).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }
}
