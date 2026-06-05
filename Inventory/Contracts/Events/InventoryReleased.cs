namespace Contracts.Events;

public class InventoryReleased
{
    public Guid OrderId { get; init; }
    public string Reason { get; init; } = default!;
    public DateTime ReleasedAt { get; init; }
    public List<InventoryReleasedLine> Lines { get; init; } = new();
}

public record InventoryReleasedLine(Guid ProductId, int Quantity);
