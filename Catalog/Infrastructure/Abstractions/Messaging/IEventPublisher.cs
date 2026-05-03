namespace Application.Abstractions.Messaging;

public interface IEventPublisher
{
    Task PublishAsync<T>(T message, string routingKey);
}