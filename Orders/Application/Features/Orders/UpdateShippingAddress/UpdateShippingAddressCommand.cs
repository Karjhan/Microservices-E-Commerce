using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Orders.UpdateShippingAddress;

public record UpdateShippingAddressCommand(
    Guid OrderId,
    string Street,
    string City,
    string State,
    string Country,
    string ZipCode,
    string? FullName,
    string? Phone) : IRequest, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "order.shipping-address.update";
    string IAuditedRequest.AuditTargetType => "Order";
    string? IAuditedRequest.AuditTargetId => OrderId.ToString();

    // Personal details are not copied into the audit trail.
    object? IAuditedRequest.AuditDetails => new { city = City, country = Country };
}