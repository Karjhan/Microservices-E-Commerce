using System.Text;
using System.Text.Json;
using Application.Features.Consumers.PaymentFailed;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Infrastructure.Messaging.Consumers;

public class PaymentFailedConsumer : BackgroundService
{
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
            exchange: "payments-exchange",
            type: ExchangeType.Topic,
            durable: true,
            cancellationToken: stoppingToken);

        var queueDeclareResult = await channel.QueueDeclareAsync(
            queue: "orders.payment-failed",
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await channel.QueueBindAsync(
            queue: queueDeclareResult.QueueName,
            exchange: "payments-exchange",
            routingKey: "payment.failed",
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                var message = JsonSerializer.Deserialize<PaymentFailedEvent>(body);

                if (message is not null)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                    await mediator.Send(new PaymentFailedCommand(
                        message.OrderId,
                        message.Reason));
                }

                await channel.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment.failed event");
                await channel.BasicNackAsync(ea.DeliveryTag, false, true);
            }
        };

        await channel.BasicConsumeAsync(
            queue: queueDeclareResult.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private record PaymentFailedEvent(Guid PaymentId, Guid OrderId, Guid UserId, decimal Amount, string Currency, string Reason, DateTime FailedAt);
}
