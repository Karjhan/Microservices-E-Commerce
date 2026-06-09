namespace Contracts.Events;

public class OrderCreated
{
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public decimal TotalAmount { get; init; }
    public string Currency { get; init; } = default!;
    public int ItemCount { get; init; }
    public DateTime CreatedAt { get; init; }

    public List<OrderCreatedLineItem> Items { get; init; } = new();
    
    public string? RecipientEmail { get; init; }
    
    public string? LineSummary { get; init; }
}

public record OrderCreatedLineItem(Guid ProductId, int Quantity);