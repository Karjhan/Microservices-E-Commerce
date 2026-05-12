namespace Infrastructure.Configuration;

public class StripeOptions
{
    public string SecretKey { get; set; } = default!;
    public string PublishableKey { get; set; } = default!;
    public string WebhookSecret { get; set; } = string.Empty;
}
