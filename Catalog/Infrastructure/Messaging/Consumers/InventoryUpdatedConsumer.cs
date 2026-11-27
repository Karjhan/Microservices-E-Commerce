using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Infrastructure.Messaging.Consumers;

/// <summary>
/// Listens to the stock changes the Inventory service publishes and moves the product between <c>Active</c> and
/// <c>OutOfStock</c> (US4, FR-016).
/// </summary>
public class InventoryUpdatedConsumer(
    IServiceScopeFactory scopeFactory,
    IConnection connection,
    ILogger<InventoryUpdatedConsumer> logger) : BackgroundService
{
    public const string Exchange = "inventory-exchange";
    public const string Queue = "catalog.inventory.updated";
    public const string RoutingKey = "inventory.updated";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, cancellationToken: stoppingToken);
        var queue = await channel.QueueDeclareAsync(Queue, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: stoppingToken);
        await channel.QueueBindAsync(queue.QueueName, Exchange, RoutingKey, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var message = JsonSerializer.Deserialize<InventoryUpdatedMessage>(
                    Encoding.UTF8.GetString(ea.Body.ToArray()), JsonOptions);

                if (message is null || message.ProductId == Guid.Empty)
                {
                    logger.LogWarning("Received an empty or unreadable {RoutingKey} payload", RoutingKey);
                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                    return;
                }

                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<StockStatusSynchronizer>()
                    .ApplyAsync(message.ProductId, message.AvailableQuantity, CancellationToken.None);

                await channel.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing {RoutingKey} event", RoutingKey);
                await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: true);
            }
        };

        await channel.BasicConsumeAsync(queue.QueueName, autoAck: false, consumer: consumer,
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private sealed class InventoryUpdatedMessage
    {
        public Guid ProductId { get; set; }
        public int AvailableQuantity { get; set; }
    }
}
