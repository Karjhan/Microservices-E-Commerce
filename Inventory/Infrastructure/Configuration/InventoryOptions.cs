namespace Infrastructure.Configuration;

public class InventoryOptions
{
    public int ReservationTimeoutMinutes { get; set; } = 30;
    public int ExpiryJobIntervalSeconds { get; set; } = 60;
    public int ExpiryJobBatchSize { get; set; } = 100;

    /// <summary>Available units at or under which a product counts as low on stock, unless it has its own threshold.</summary>
    public int DefaultLowStockThreshold { get; set; } = 5;
}
