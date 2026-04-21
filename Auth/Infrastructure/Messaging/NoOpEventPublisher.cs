using Domain.Abstractions.Messaging;

namespace Infrastructure.Messaging;

public class NoOpEventPublisher : IEventPublisher
{
    public Task PublishAsync<T>(T @event, string routingKey, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}