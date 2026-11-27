using Domain.Entities;

namespace Application.DTOs;

public class InventoryItemDto
{
    public Guid ProductId { get; init; }
    public int OnHandQuantity { get; init; }
    public int ReservedQuantity { get; init; }
    public int AvailableQuantity { get; init; }
    public bool IsDepleted { get; init; }

    public int LowStockThreshold { get; init; }

    public string Status { get; init; } = "ok";

    public string Version { get; init; } = string.Empty;

    public DateTime UpdatedAt { get; init; }

    public static InventoryItemDto FromEntity(InventoryItem item) => new()
    {
        ProductId = item.ProductId,
        OnHandQuantity = item.OnHandQuantity,
        ReservedQuantity = item.ReservedQuantity,
        AvailableQuantity = item.AvailableQuantity,
        IsDepleted = item.IsDepleted,
        LowStockThreshold = item.LowStockThreshold,
        Status = item.Status.ToString().ToLowerInvariant(),
        Version = Convert.ToBase64String(item.RowVersion),
        UpdatedAt = item.UpdatedAt
    };
}

public class PublicInventoryDto
{
    public Guid ProductId { get; init; }
    public int Available { get; init; }
    public bool InStock { get; init; }

    public static PublicInventoryDto FromDto(InventoryItemDto item) => new()
    {
        ProductId = item.ProductId,
        Available = Math.Max(0, item.AvailableQuantity),
        InStock = item.AvailableQuantity > 0
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

public class InventoryMovementDto
{
    public Guid Id { get; init; }
    public DateTime OccurredAt { get; init; }

    public string Type { get; init; } = string.Empty;

    public int Delta { get; init; }
    public string? Reason { get; init; }

    public Guid? ActorId { get; init; }
    public string? Actor { get; init; }

    public Guid? OrderId { get; init; }
    public int ResultingOnHand { get; init; }
    public int ReservedAfter { get; init; }

    public static InventoryMovementDto FromEntity(InventoryMovement m) => new()
    {
        Id = m.Id,
        OccurredAt = m.OccurredAt,
        Type = m.MovementType.ToString(),
        Delta = m.QuantityDelta,
        Reason = m.Reason,
        ActorId = m.ActorId,
        Actor = m.ActorEmail,
        OrderId = m.OrderId,
        ResultingOnHand = m.OnHandAfter,
        ReservedAfter = m.ReservedAfter
    };
}

public class InventoryMovementListDto
{
    public List<InventoryMovementDto> Items { get; init; } = new();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
}
