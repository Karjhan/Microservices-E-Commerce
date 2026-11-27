using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Payments.RefundPayment;

public record RefundPaymentCommand(Guid PaymentId, decimal? Amount = null) : IRequest, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "payment.refund";
    string IAuditedRequest.AuditTargetType => "Payment";
    string? IAuditedRequest.AuditTargetId => PaymentId.ToString();
    object? IAuditedRequest.AuditDetails => new { amount = Amount, full = Amount is null };
}