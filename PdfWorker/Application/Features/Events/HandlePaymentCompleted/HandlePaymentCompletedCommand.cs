using Contracts.Events;
using MediatR;

namespace Application.Features.Events.HandlePaymentCompleted;

public record HandlePaymentCompletedCommand(PaymentCompleted Payment, string? CorrelationId) : IRequest;
