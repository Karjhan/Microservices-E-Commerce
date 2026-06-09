using Domain.Enums;
using Domain.Exceptions;

namespace Domain.Entities;

public class Order
{
    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public OrderStatus Status { get; private set; }

    public decimal TotalAmount { get; private set; }

    public string Currency { get; private set; } = "EUR";

    public string? Notes { get; private set; }

    public ShippingAddress ShippingAddress { get; private set; } = null!;

    public List<OrderItem> Items { get; private set; } = new();

    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public DateTime? ShippedAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }

    public string? CancellationReason { get; private set; }

    public string? RecipientEmail {get; private set;}
    
    private Order() { }

    public Order(
        Guid userId,
        string currency,
        string? notes = null)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Currency = currency;
        Notes = notes;
        Status = OrderStatus.PendingPayment;
        TotalAmount = 0;
        CreatedAt = DateTime.UtcNow;
    }

    public void AddItem(OrderItem item)
    {
        Items.Add(item);
        RecalculateTotal();
    }

    public void SetShippingAddress(ShippingAddress address)
    {
        ShippingAddress = address;
    }

    public void MarkPaid()
    {
        if (Status != OrderStatus.PendingPayment)
            throw new InvalidOrderStateException(
                $"Cannot mark order as paid in '{Status}' state. Expected 'PendingPayment'.");

        Status = OrderStatus.Paid;
        PaidAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetRecipientEmail(string? email)
    {
        RecipientEmail = email;
    }

    public void MarkProcessing()
    {
        if (Status != OrderStatus.Paid)
            throw new InvalidOrderStateException(
                $"Cannot mark order as processing in '{Status}' state. Expected 'Paid'.");

        Status = OrderStatus.Processing;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkShipped()
    {
        if (Status != OrderStatus.Processing)
            throw new InvalidOrderStateException(
                $"Cannot mark order as shipped in '{Status}' state. Expected 'Processing'.");

        Status = OrderStatus.Shipped;
        ShippedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkDelivered()
    {
        if (Status != OrderStatus.Shipped)
            throw new InvalidOrderStateException(
                $"Cannot mark order as delivered in '{Status}' state. Expected 'Shipped'.");

        Status = OrderStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkCancelled(string reason)
    {
        if (Status == OrderStatus.Shipped || Status == OrderStatus.Delivered)
            throw new InvalidOrderStateException(
                $"Cannot cancel order in '{Status}' state.");

        Status = OrderStatus.Cancelled;
        CancellationReason = reason;
        CancelledAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkPaymentFailed()
    {
        if (Status != OrderStatus.PendingPayment)
            throw new InvalidOrderStateException(
                $"Cannot mark payment as failed in '{Status}' state. Expected 'PendingPayment'.");

        Status = OrderStatus.PaymentFailed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateShippingAddress(
        string street, string city, string state,
        string country, string zipCode,
        string? fullName = null, string? phone = null)
    {
        if (Status != OrderStatus.PendingPayment && Status != OrderStatus.Paid)
            throw new InvalidOrderStateException(
                $"Cannot update shipping address in '{Status}' state.");

        ShippingAddress.Update(street, city, state, country, zipCode, fullName, phone);
        UpdatedAt = DateTime.UtcNow;
    }

    private void RecalculateTotal()
    {
        TotalAmount = Items.Sum(i => i.LineTotal);
    }
}
