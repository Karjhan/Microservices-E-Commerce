using Application.Features.Events.HandlePaymentCompleted;
using Contracts.Events;
using Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Infrastructure.Messaging.Consumers;

public class PaymentCompletedConsumer : ConsumerBase<PaymentCompleted, HandlePaymentCompletedCommand>
{
    public PaymentCompletedConsumer(
        IServiceScopeFactory scopeFactory,
        IConnection connection,
        ILogger<PaymentCompletedConsumer> logger)
        : base(scopeFactory, connection, logger) { }

    protected override string Exchange => "payments-exchange";
    protected override string Queue => "pdf-worker.payment-completed";
    protected override string RoutingKey => "payment.completed";
    protected override string ConsumerName => nameof(PaymentCompletedConsumer);

    protected override HandlePaymentCompletedCommand BuildCommand(PaymentCompleted message, string? correlationId) =>
        new(message, correlationId);
}
