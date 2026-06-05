using Domain.Exceptions;

namespace Domain.Entities;

public class InventoryItem
{
    public Guid Id { get; private set; }

    public Guid ProductId { get; private set; }

    public int OnHandQuantity { get; private set; }
    public int ReservedQuantity { get; private set; }

    public int AvailableQuantity => OnHandQuantity - ReservedQuantity;
    public bool IsDepleted => AvailableQuantity <= 0;

    public byte[] RowVersion { get; private set; } = Guid.NewGuid().ToByteArray();

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private InventoryItem() { }

    public InventoryItem(Guid productId, int initialOnHand = 0)
    {
        if (initialOnHand < 0)
            throw new InvalidAdjustmentException("Initial on-hand quantity cannot be negative.");

        Id = Guid.NewGuid();
        ProductId = productId;
        OnHandQuantity = initialOnHand;
        ReservedQuantity = 0;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        RowVersion = Guid.NewGuid().ToByteArray();
    }

    public void Reserve(int quantity)
    {
        if (quantity <= 0)
            throw new InvalidReservationStateException("Reservation quantity must be greater than zero.");

        if (AvailableQuantity < quantity)
            throw new InsufficientStockException(ProductId, quantity, AvailableQuantity);

        ReservedQuantity += quantity;
        Touch();
    }

    public void Release(int quantity)
    {
        if (quantity <= 0)
            throw new InvalidReservationStateException("Release quantity must be greater than zero.");

        if (quantity > ReservedQuantity)
            throw new InvalidReservationStateException(
                $"Cannot release {quantity}; only {ReservedQuantity} reserved for product {ProductId}.");

        ReservedQuantity -= quantity;
        Touch();
    }

    public void Deduct(int quantity)
    {
        if (quantity <= 0)
            throw new InvalidReservationStateException("Deduction quantity must be greater than zero.");

        if (quantity > ReservedQuantity)
            throw new InvalidReservationStateException(
                $"Cannot deduct {quantity}; only {ReservedQuantity} reserved for product {ProductId}.");

        if (quantity > OnHandQuantity)
            throw new InvalidReservationStateException(
                $"Cannot deduct {quantity}; only {OnHandQuantity} on-hand for product {ProductId}.");

        ReservedQuantity -= quantity;
        OnHandQuantity -= quantity;
        Touch();
    }

    public void Adjust(int delta)
    {
        var projected = OnHandQuantity + delta;

        if (projected < 0)
            throw new InvalidAdjustmentException(
                $"Adjustment {delta} would make on-hand quantity negative for product {ProductId}.");

        if (projected < ReservedQuantity)
            throw new InvalidAdjustmentException(
                $"Adjustment {delta} would make on-hand ({projected}) less than reserved ({ReservedQuantity}) for product {ProductId}.");

        OnHandQuantity = projected;
        Touch();
    }

    private void Touch()
    {
        UpdatedAt = DateTime.UtcNow;
        RowVersion = Guid.NewGuid().ToByteArray();
    }
}
