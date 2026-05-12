using MediatR;

namespace Application.Features.Payments.RefundPayment;

public record RefundPaymentCommand(Guid PaymentId, decimal? Amount = null) : IRequest;
