using Application.Features.Events.HandlePaymentRefunded;
using Contracts.Events;
using Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Infrastructure.Messaging.Consumers;

public class PaymentRefundedConsumer : ConsumerBase<PaymentRefunded, HandlePaymentRefundedCommand>
{
    public PaymentRefundedConsumer(
        IServiceScopeFactory scopeFactory,
        IConnection connection,
        ILogger<PaymentRefundedConsumer> logger)
        : base(scopeFactory, connection, logger) { }

    protected override string Exchange => "payments-exchange";
    protected override string Queue => "pdf-worker.payment-refunded";
    protected override string RoutingKey => "payment.refunded";
    protected override string ConsumerName => nameof(PaymentRefundedConsumer);

    protected override HandlePaymentRefundedCommand BuildCommand(PaymentRefunded message, string? correlationId) =>
        new(message, correlationId);
}
