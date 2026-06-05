using System.Text;
using System.Text.Json;
using Application.Abstractions.Persistence;
using Application.Features.Inventory.DeductStock;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Infrastructure.Messaging.Consumers;

public class PaymentCompletedConsumer : BackgroundService
{
    private const string Exchange = "payments-exchange";
    private const string Queue = "inventory.payments.completed";
    private const string RoutingKey = "payment.completed";
    private const string ConsumerName = nameof(PaymentCompletedConsumer);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnection _connection;
    private readonly ILogger<PaymentCompletedConsumer> _logger;

    public PaymentCompletedConsumer(
        IServiceScopeFactory scopeFactory,
        IConnection connection,
        ILogger<PaymentCompletedConsumer> logger)
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
                var message = JsonSerializer.Deserialize<PaymentCompletedMessage>(body, JsonOptions);

                if (message is null)
                {
                    _logger.LogWarning("Received empty {RoutingKey} payload", RoutingKey);
                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                    return;
                }

                var correlationId = ea.BasicProperties?.CorrelationId;
                var idempotencyKey = $"payment.completed:{message.OrderId}";

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
                    new DeductStockCommand(message.OrderId, correlationId));

                _logger.LogInformation(
                    "Deduction outcome for order {OrderId}: success={Success} reason={Reason}",
                    message.OrderId, result.Success, result.Reason ?? "ok");

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

    private sealed class PaymentCompletedMessage
    {
        public Guid PaymentId { get; set; }
        public Guid OrderId { get; set; }
        public Guid UserId { get; set; }
        public decimal Amount { get; set; }
        public string? Currency { get; set; }
        public string? ProviderPaymentId { get; set; }
        public DateTime CompletedAt { get; set; }
    }
}
