namespace Infrastructure.Configuration;

public class EnrichmentOptions
{
    public int OrdersTimeoutSeconds { get; set; } = 3;
    public int RetryCount { get; set; } = 2;
}
