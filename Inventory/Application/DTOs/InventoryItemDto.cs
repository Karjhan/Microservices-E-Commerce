using Domain.Entities;

namespace Application.DTOs;

public class InventoryItemDto
{
    public Guid ProductId { get; init; }
    public int OnHandQuantity { get; init; }
    public int ReservedQuantity { get; init; }
    public int AvailableQuantity { get; init; }
    public bool IsDepleted { get; init; }
    public DateTime UpdatedAt { get; init; }

    public static InventoryItemDto FromEntity(InventoryItem item) => new()
    {
        ProductId = item.ProductId,
        OnHandQuantity = item.OnHandQuantity,
        ReservedQuantity = item.ReservedQuantity,
        AvailableQuantity = item.AvailableQuantity,
        IsDepleted = item.IsDepleted,
        UpdatedAt = item.UpdatedAt
    };
}

public class InventoryListDto
{
    public List<InventoryItemDto> Items { get; init; } = new();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
}
