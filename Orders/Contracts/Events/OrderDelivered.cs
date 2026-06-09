namespace Contracts.Events;

public class OrderDelivered
{
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public DateTime DeliveredAt { get; init; }
    public string? RecipientEmail {get; init;}
}
