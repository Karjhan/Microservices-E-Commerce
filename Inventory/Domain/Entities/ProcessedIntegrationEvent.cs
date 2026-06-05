namespace Domain.Entities;

public class ProcessedIntegrationEvent
{
    public Guid Id { get; private set; }

    public string ConsumerName { get; private set; } = default!;

    public string IdempotencyKey { get; private set; } = default!;

    public DateTime ProcessedAt { get; private set; }

    private ProcessedIntegrationEvent() { }

    public ProcessedIntegrationEvent(string consumerName, string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(consumerName))
            throw new ArgumentException("Consumer name is required.", nameof(consumerName));
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));

        Id = Guid.NewGuid();
        ConsumerName = consumerName;
        IdempotencyKey = idempotencyKey;
        ProcessedAt = DateTime.UtcNow;
    }
}
