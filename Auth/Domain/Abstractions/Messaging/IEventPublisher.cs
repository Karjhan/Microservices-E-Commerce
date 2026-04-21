namespace Domain.Abstractions.Messaging;

public interface IEventPublisher
{
    Task PublishAsync<T>(T @event, string routingKey, CancellationToken ct = default);
}