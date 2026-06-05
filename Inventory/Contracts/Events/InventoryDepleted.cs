namespace Contracts.Events;

public class InventoryDepleted
{
    public Guid ProductId { get; init; }
    public DateTime DepletedAt { get; init; }
}
