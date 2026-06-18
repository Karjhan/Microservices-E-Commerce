using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Application.Abstractions.Messaging;
using PdfWorker.Contracts;
using RabbitMQ.Client;

namespace Infrastructure.Messaging;

public class RabbitMqPublisher : IEventPublisher
{
    public const string ExchangeName = "documents-exchange";

    private readonly IConnection _connection;

    public RabbitMqPublisher(IConnection connection)
    {
        _connection = connection;
    }

    public async Task EnsureTopologyAsync(CancellationToken cancellationToken = default)
    {
        await using var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            cancellationToken: cancellationToken);
    }

    public async Task PublishAsync<T>(
        T message,
        string routingKey,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySources.Messaging.StartActivity("rabbitmq.publish");

        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination", routingKey);

        await using var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            cancellationToken: cancellationToken);

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            CorrelationId = correlationId,
            Headers = correlationId is null
                ? null
                : new Dictionary<string, object?> { ["X-Correlation-Id"] = correlationId }
        };

        await channel.BasicPublishAsync(
            exchange: ExchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        activity?.SetStatus(ActivityStatusCode.Ok);
    }
}
