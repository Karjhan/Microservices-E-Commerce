using MediatR;

namespace Application.Features.Payments.ProcessPayment;

public record ProcessPaymentCommand(Guid PaymentId) : IRequest;
