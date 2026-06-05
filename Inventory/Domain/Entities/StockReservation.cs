using Domain.Enums;
using Domain.Exceptions;

namespace Domain.Entities;

public class StockReservation
{
    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }
    public Guid? OrderLineId { get; private set; }
    public Guid ProductId { get; private set; }

    public int Quantity { get; private set; }

    public ReservationStatus Status { get; private set; }

    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? FinalizedAt { get; private set; }

    private StockReservation() { }

    public StockReservation(
        Guid orderId,
        Guid productId,
        int quantity,
        DateTime expiresAt,
        Guid? orderLineId = null)
    {
        if (quantity <= 0)
            throw new InvalidReservationStateException("Reservation quantity must be greater than zero.");

        Id = Guid.NewGuid();
        OrderId = orderId;
        OrderLineId = orderLineId;
        ProductId = productId;
        Quantity = quantity;
        Status = ReservationStatus.Active;
        ExpiresAt = expiresAt;
        CreatedAt = DateTime.UtcNow;
    }

    public void Release()
    {
        Transition(ReservationStatus.Released);
    }

    public void Expire()
    {
        Transition(ReservationStatus.Expired);
    }

    public void Deduct()
    {
        Transition(ReservationStatus.Deducted);
    }

    public bool IsActive => Status == ReservationStatus.Active;
    public bool IsTerminal => Status != ReservationStatus.Active;

    private void Transition(ReservationStatus next)
    {
        if (Status != ReservationStatus.Active)
            throw new InvalidReservationStateException(
                $"Cannot transition reservation {Id} from '{Status}' to '{next}'.");

        Status = next;
        FinalizedAt = DateTime.UtcNow;
    }
}
