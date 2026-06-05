using Domain.Enums;

namespace Domain.Entities;

public class InventoryMovement
{
    public Guid Id { get; private set; }

    public Guid ProductId { get; private set; }

    public Guid? OrderId { get; private set; }
    public Guid? ReservationId { get; private set; }

    public MovementType MovementType { get; private set; }

    public int QuantityDelta { get; private set; }

    public int OnHandAfter { get; private set; }
    public int ReservedAfter { get; private set; }

    public string SourceTrigger { get; private set; } = default!;
    public string? Reason { get; private set; }
    public string? CorrelationId { get; private set; }

    public DateTime OccurredAt { get; private set; }

    private InventoryMovement() { }

    public InventoryMovement(
        Guid productId,
        MovementType movementType,
        int quantityDelta,
        int onHandAfter,
        int reservedAfter,
        string sourceTrigger,
        Guid? orderId = null,
        Guid? reservationId = null,
        string? reason = null,
        string? correlationId = null)
    {
        Id = Guid.NewGuid();
        ProductId = productId;
        OrderId = orderId;
        ReservationId = reservationId;
        MovementType = movementType;
        QuantityDelta = quantityDelta;
        OnHandAfter = onHandAfter;
        ReservedAfter = reservedAfter;
        SourceTrigger = sourceTrigger;
        Reason = reason;
        CorrelationId = correlationId;
        OccurredAt = DateTime.UtcNow;
    }
}
