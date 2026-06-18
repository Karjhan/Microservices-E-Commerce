using Contracts.Events;
using MediatR;

namespace Application.Features.Events.HandlePaymentRefunded;

public record HandlePaymentRefundedCommand(PaymentRefunded Payment, string? CorrelationId) : IRequest;
