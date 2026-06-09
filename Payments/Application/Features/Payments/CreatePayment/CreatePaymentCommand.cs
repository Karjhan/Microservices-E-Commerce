using Domain.Enums;
using MediatR;

namespace Application.Features.Payments.CreatePayment;

public record CreatePaymentCommand(
    Guid OrderId,
    Guid UserId,
    decimal Amount,
    string Currency,
    PaymentMethod Method,
    string IdempotencyKey,
    string PaymentToken,
    string? RecipientEmail = null
) : IRequest<Guid>;
