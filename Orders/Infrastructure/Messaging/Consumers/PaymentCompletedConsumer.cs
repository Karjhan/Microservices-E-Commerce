    using System.Text;
    using System.Text.Json;
    using Application.Features.Consumers.PaymentCompleted;
    using MediatR;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using RabbitMQ.Client;
    using RabbitMQ.Client.Events;

    namespace Infrastructure.Messaging.Consumers;

    public class PaymentCompletedConsumer : BackgroundService
    {
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
                exchange: "payments-exchange",
                type: ExchangeType.Topic,
                durable: true,
                cancellationToken: stoppingToken);

            var queueDeclareResult = await channel.QueueDeclareAsync(
                queue: "orders.payment-completed",
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: stoppingToken);

            await channel.QueueBindAsync(
                queue: queueDeclareResult.QueueName,
                exchange: "payments-exchange",
                routingKey: "payment.completed",
                cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (_, ea) =>
            {
                try
                {
                    var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var message = JsonSerializer.Deserialize<PaymentCompletedEvent>(body);

                    if (message is not null)
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                        await mediator.Send(new PaymentCompletedCommand(
                            message.OrderId,
                            message.CompletedAt));
                    }

                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing payment.completed event");
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

        private sealed class PaymentCompletedEvent
        {
            public Guid PaymentId { get; set; }
            public Guid OrderId { get; set; }
            public Guid UserId { get; set; }
            public decimal Amount { get; set; }
            public string Currency { get; set; } = default!;
            public string ProviderPaymentId { get; set; } = default!;
            public DateTime CompletedAt { get; set; }
        }
    }
