using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Infrastructure.Messaging;

/// <summary>
/// Base BackgroundService for RabbitMQ consumers. Declares the source exchange,
/// binds a durable queue with the supplied routing key, deserializes JSON
/// payloads, dispatches via MediatR, and uses manual acknowledgement.
/// </summary>
public abstract class ConsumerBase<TMessage, TCommand> : BackgroundService
    where TCommand : IRequest
{
    protected readonly IServiceScopeFactory ScopeFactory;
    protected readonly IConnection Connection;
    protected readonly ILogger Logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected ConsumerBase(
        IServiceScopeFactory scopeFactory,
        IConnection connection,
        ILogger logger)
    {
        ScopeFactory = scopeFactory;
        Connection = connection;
        Logger = logger;
    }

    protected abstract string Exchange { get; }
    protected abstract string Queue { get; }
    protected abstract string RoutingKey { get; }
    protected abstract string ConsumerName { get; }

    protected abstract TCommand BuildCommand(TMessage message, string? correlationId);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = await Connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.ExchangeDeclareAsync(
            exchange: Exchange,
            type: ExchangeType.Topic,
            durable: true,
            cancellationToken: stoppingToken);

        var queueDeclareResult = await channel.QueueDeclareAsync(
            queue: Queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await channel.QueueBindAsync(
            queue: queueDeclareResult.QueueName,
            exchange: Exchange,
            routingKey: RoutingKey,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                var message = JsonSerializer.Deserialize<TMessage>(body, JsonOptions);

                if (message is null)
                {
                    Logger.LogWarning(
                        "{ConsumerName} received empty or unparseable payload on {RoutingKey}",
                        ConsumerName, RoutingKey);

                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                    return;
                }

                var correlationId = TryReadCorrelationId(ea);

                using var scope = ScopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                var command = BuildCommand(message, correlationId);
                await mediator.Send(command);

                await channel.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    ex,
                    "{ConsumerName} failed processing message on {RoutingKey}",
                    ConsumerName,
                    RoutingKey);

                await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: true);
            }
        };

        await channel.BasicConsumeAsync(
            queue: queueDeclareResult.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private static string? TryReadCorrelationId(BasicDeliverEventArgs ea)
    {
        if (ea.BasicProperties is null)
            return null;

        if (!string.IsNullOrEmpty(ea.BasicProperties.CorrelationId))
            return ea.BasicProperties.CorrelationId;

        if (ea.BasicProperties.Headers is { } headers
            && headers.TryGetValue("X-Correlation-Id", out var value)
            && value is byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        return null;
    }
}
