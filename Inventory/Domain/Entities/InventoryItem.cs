using Domain.Enums;
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

    /// <summary>At or under this many available units the product counts as low on stock (FR-028).</summary>
    public int LowStockThreshold { get; private set; } = DefaultLowStockThreshold;

    public StockStatus Status =>
        IsDepleted ? StockStatus.Out : AvailableQuantity <= LowStockThreshold ? StockStatus.Low : StockStatus.Ok;

    public const int DefaultLowStockThreshold = 5;
    public const int MaxLowStockThreshold = 100_000;
    public const int MaxQuantity = 1_000_000;

    public byte[] RowVersion { get; private set; } = Guid.NewGuid().ToByteArray();

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private InventoryItem() { }

    public InventoryItem(Guid productId, int initialOnHand = 0, int lowStockThreshold = DefaultLowStockThreshold)
    {
        if (initialOnHand < 0)
            throw new InvalidAdjustmentException("Initial on-hand quantity cannot be negative.", InvalidAdjustmentException.BelowZeroCode);

        EnsureValidThreshold(lowStockThreshold);

        Id = Guid.NewGuid();
        ProductId = productId;
        OnHandQuantity = initialOnHand;
        ReservedQuantity = 0;
        LowStockThreshold = lowStockThreshold;
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

    /// <summary>Adds (or, when negative, removes) units. Refused when the result is below zero or below what is reserved.</summary>
    public void Adjust(int delta)
    {
        SetOnHandChecked((long)OnHandQuantity + delta, $"Adjustment {delta}");
    }

    /// <summary>Sets the exact on-hand count and returns the change it made. Same limits as <see cref="Adjust"/>.</summary>
    public int SetOnHand(int quantity)
    {
        var delta = quantity - OnHandQuantity;
        SetOnHandChecked(quantity, $"Setting the count to {quantity}");
        return delta;
    }

    public void SetLowStockThreshold(int threshold)
    {
        EnsureValidThreshold(threshold);
        LowStockThreshold = threshold;
        Touch();
    }

    private void SetOnHandChecked(long projected, string what)
    {
        if (projected < 0)
            throw new InvalidAdjustmentException(
                $"{what} would make on-hand quantity negative for product {ProductId}.",
                InvalidAdjustmentException.BelowZeroCode);

        if (projected < ReservedQuantity)
            throw new InvalidAdjustmentException(
                $"{what} would make on-hand ({projected}) less than reserved ({ReservedQuantity}) for product {ProductId}.",
                InvalidAdjustmentException.BelowReservedCode);

        if (projected > MaxQuantity)
            throw new InvalidAdjustmentException(
                $"{what} would exceed the largest allowed quantity ({MaxQuantity}) for product {ProductId}.");

        OnHandQuantity = (int)projected;
        Touch();
    }

    private static void EnsureValidThreshold(int threshold)
    {
        if (threshold < 0 || threshold > MaxLowStockThreshold)
            throw new InvalidAdjustmentException(
                $"Low-stock threshold must be between 0 and {MaxLowStockThreshold}.");
    }
    private void Touch()
    {
        UpdatedAt = DateTime.UtcNow;
        RowVersion = Guid.NewGuid().ToByteArray();
    }
}
