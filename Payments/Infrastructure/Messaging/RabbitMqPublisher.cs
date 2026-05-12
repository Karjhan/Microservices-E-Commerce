using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Application.Abstractions.Messaging;
using Contracts;
using RabbitMQ.Client;

namespace Infrastructure.Messaging;

public class RabbitMqPublisher : IEventPublisher
{
    private readonly IConnection _connection;

    public RabbitMqPublisher(IConnection connection)
    {
        _connection = connection;
    }

    public async Task PublishAsync<T>(T message, string routingKey)
    {
        using var activity = ActivitySources.Messaging.StartActivity("rabbitmq.publish");

        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination", routingKey);

        await using var channel = await _connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(
            exchange: "payments-exchange",
            type: ExchangeType.Topic,
            durable: true
        );

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

        await channel.BasicPublishAsync(
            exchange: "payments-exchange",
            routingKey: routingKey,
            mandatory: false,
            body: body
        );

        activity?.SetStatus(ActivityStatusCode.Ok);
    }
}
