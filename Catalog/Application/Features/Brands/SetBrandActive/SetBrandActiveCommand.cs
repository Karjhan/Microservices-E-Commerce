using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Brands.SetBrandActive;

public record SetBrandActiveCommand(Guid Id, bool Active) : IRequest, IAuditedRequest
{
    string IAuditedRequest.AuditAction => Active ? "brand.activate" : "brand.deactivate";
    string IAuditedRequest.AuditTargetType => "Brand";
    string? IAuditedRequest.AuditTargetId => Id.ToString();
    object? IAuditedRequest.AuditDetails => null;
}
