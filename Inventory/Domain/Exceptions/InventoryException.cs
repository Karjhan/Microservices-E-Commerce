namespace Domain.Exceptions;

public abstract class InventoryException : Exception
{
    public string Code { get; }

    protected InventoryException(string message, string code) : base(message)
    {
        Code = code;
    }
}

public sealed class InventoryItemNotFoundException : InventoryException
{
    public InventoryItemNotFoundException(Guid productId)
        : base($"Inventory item for product {productId} not found", "inventory.not_found") { }
}

public sealed class InsufficientStockException : InventoryException
{
    public Guid ProductId { get; }
    public int Requested { get; }
    public int Available { get; }

    public InsufficientStockException(Guid productId, int requested, int available)
        : base(
            $"Insufficient stock for product {productId}: requested {requested}, available {available}",
            "inventory.insufficient_stock")
    {
        ProductId = productId;
        Requested = requested;
        Available = available;
    }
}

public sealed class InvalidReservationStateException : InventoryException
{
    public InvalidReservationStateException(string message)
        : base(message, "inventory.invalid_reservation_state") { }
}

public sealed class InvalidAdjustmentException : InventoryException
{
    public InvalidAdjustmentException(string message)
        : base(message, "inventory.invalid_adjustment") { }
}

public sealed class ConcurrencyConflictException : InventoryException
{
    public ConcurrencyConflictException(string message)
        : base(message, "inventory.concurrency_conflict") { }
}
