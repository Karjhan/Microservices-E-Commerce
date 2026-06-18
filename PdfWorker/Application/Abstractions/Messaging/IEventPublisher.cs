namespace Application.Abstractions.Messaging;

public interface IEventPublisher
{
    Task PublishAsync<T>(T message, string routingKey, string? correlationId = null, CancellationToken cancellationToken = default);
    Task EnsureTopologyAsync(CancellationToken cancellationToken = default);
}
