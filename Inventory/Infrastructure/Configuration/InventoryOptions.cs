namespace Infrastructure.Configuration;

public class InventoryOptions
{
    public int ReservationTimeoutMinutes { get; set; } = 30;
    public int ExpiryJobIntervalSeconds { get; set; } = 60;
    public int ExpiryJobBatchSize { get; set; } = 100;
}
