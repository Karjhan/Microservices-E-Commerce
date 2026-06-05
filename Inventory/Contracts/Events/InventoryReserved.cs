namespace Contracts.Events;

public class InventoryReserved
{
    public Guid OrderId { get; init; }
    public DateTime ReservedAt { get; init; }
    public List<InventoryReservedLine> Lines { get; init; } = new();
}

public record InventoryReservedLine(Guid ProductId, int Quantity);
