namespace Domain.Exceptions;

public abstract class InventoryException : Exception
{
    public string Code { get; }

    public IReadOnlyDictionary<string, string[]>? Errors { get; }

    protected InventoryException(string message, string code, IReadOnlyDictionary<string, string[]>? errors = null)
        : base(message)
    {
        Code = code;
        Errors = errors;
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
    public const string GenericCode = "inventory.invalid_adjustment";
    public const string BelowZeroCode = "below_zero";
    public const string BelowReservedCode = "below_reserved";

    public InvalidAdjustmentException(string message, string code = GenericCode)
        : base(message, code) { }
}

public sealed class InventoryValidationException : InventoryException
{
    public InventoryValidationException(string field, string message)
        : base(message, "validation_failed", new Dictionary<string, string[]> { [field] = [message] }) { }
}

public sealed class ConcurrencyConflictException : InventoryException
{
    public const string StaleVersionCode = "stale_version";

    public ConcurrencyConflictException(string message)
        : base(message, StaleVersionCode) { }
}
