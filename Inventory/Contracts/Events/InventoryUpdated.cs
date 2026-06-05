namespace Contracts.Events;

public class InventoryUpdated
{
    public Guid ProductId { get; init; }
    public int OnHandQuantity { get; init; }
    public int ReservedQuantity { get; init; }
    public int AvailableQuantity { get; init; }
    public DateTime UpdatedAt { get; init; }
}
