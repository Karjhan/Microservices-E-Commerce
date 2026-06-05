using System.Text;
using System.Text.Json;
using Application.Abstractions.Persistence;
using Application.Features.Inventory.ReserveStock;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Infrastructure.Messaging.Consumers;

public class OrderCreatedConsumer : BackgroundService
{
    private const string Exchange = "orders-exchange";
    private const string Queue = "inventory.orders.created";
    private const string RoutingKey = "order.created";
    private const string ConsumerName = nameof(OrderCreatedConsumer);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnection _connection;
    private readonly ILogger<OrderCreatedConsumer> _logger;

    public OrderCreatedConsumer(
        IServiceScopeFactory scopeFactory,
        IConnection connection,
        ILogger<OrderCreatedConsumer> logger)
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
                var message = JsonSerializer.Deserialize<OrderCreatedMessage>(body, JsonOptions);

                if (message is null)
                {
                    _logger.LogWarning("Received empty {RoutingKey} payload", RoutingKey);
                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                    return;
                }

                var correlationId = ea.BasicProperties?.CorrelationId;
                var idempotencyKey = $"order.created:{message.OrderId}";

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

                var lines = (message.Items ?? new List<OrderCreatedItemMessage>())
                    .Where(i => i.Quantity > 0)
                    .Select(i => new ReserveStockLine(i.ProductId, i.Quantity))
                    .ToList();

                if (lines.Count == 0)
                {
                    _logger.LogInformation(
                        "Order {OrderId} has no items; nothing to reserve.",
                        message.OrderId);
                }
                else
                {
                    var result = await mediator.Send(
                        new ReserveStockCommand(message.OrderId, lines, correlationId));

                    _logger.LogInformation(
                        "Reservation outcome for order {OrderId}: success={Success} reason={Reason}",
                        message.OrderId, result.Success, result.Reason ?? "ok");
                }

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

    private sealed class OrderCreatedMessage
    {
        public Guid OrderId { get; set; }
        public Guid UserId { get; set; }
        public decimal TotalAmount { get; set; }
        public string? Currency { get; set; }
        public int ItemCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<OrderCreatedItemMessage>? Items { get; set; }
    }

    private sealed class OrderCreatedItemMessage
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
    }
}
