using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Orders.CancelOrder;

public record CancelOrderCommand(Guid OrderId, string Reason) : IRequest, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "order.cancel";
    string IAuditedRequest.AuditTargetType => "Order";
    string? IAuditedRequest.AuditTargetId => OrderId.ToString();
    object? IAuditedRequest.AuditDetails => new { reason = Reason };
}