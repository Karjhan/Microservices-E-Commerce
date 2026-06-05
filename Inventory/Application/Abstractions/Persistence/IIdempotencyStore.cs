namespace Application.Abstractions.Persistence;

public interface IIdempotencyStore
{
    Task<bool> HasProcessedAsync(string consumerName, string idempotencyKey, CancellationToken ct);

    Task MarkProcessedAsync(string consumerName, string idempotencyKey, CancellationToken ct);
}
