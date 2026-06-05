using System.Text;
using System.Text.Json;
using Application.Abstractions.Persistence;
using Application.Features.Inventory.ReleaseStock;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Infrastructure.Messaging.Consumers;

public class PaymentFailedConsumer : BackgroundService
{
    private const string Exchange = "payments-exchange";
    private const string Queue = "inventory.payments.failed";
    private const string RoutingKey = "payment.failed";
    private const string ConsumerName = nameof(PaymentFailedConsumer);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnection _connection;
    private readonly ILogger<PaymentFailedConsumer> _logger;

    public PaymentFailedConsumer(
        IServiceScopeFactory scopeFactory,
        IConnection connection,
        ILogger<PaymentFailedConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _connection = connection;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

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
                var message = JsonSerializer.Deserialize<PaymentFailedMessage>(body, JsonOptions);

                if (message is null)
                {
                    _logger.LogWarning("Received empty {RoutingKey} payload", RoutingKey);
                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                    return;
                }

                var correlationId = ea.BasicProperties?.CorrelationId;
                var idempotencyKey = $"payment.failed:{message.OrderId}";

                using var scope = _scopeFactory.CreateScope();
                var idempotency = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                if (await idempotency.HasProcessedAsync(ConsumerName, idempotencyKey, default))
                {
                    _logger.LogInformation(
                        "{ConsumerName} skipping duplicate event {IdempotencyKey}",
                        ConsumerName, idempotencyKey);

                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                    return;
                }

                var result = await mediator.Send(
                    new ReleaseStockCommand(message.OrderId, "payment.failed", correlationId));

                _logger.LogInformation(
                    "Release outcome for order {OrderId}: released={Released} note={Note}",
                    message.OrderId, result.ReleasedCount, result.Note ?? "ok");

                await idempotency.MarkProcessedAsync(ConsumerName, idempotencyKey, default);

                var repo = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();
                await repo.SaveChangesAsync(default);

                await channel.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing {RoutingKey} event", RoutingKey);
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

    private sealed class PaymentFailedMessage
    {
        public Guid PaymentId { get; set; }
        public Guid OrderId { get; set; }
        public Guid UserId { get; set; }
        public decimal Amount { get; set; }
        public string? Currency { get; set; }
        public string? Reason { get; set; }
        public DateTime FailedAt { get; set; }
    }
}
