using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Brands.UpdateBrand;

public record UpdateBrandCommand(Guid Id, string Name) : IRequest, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "brand.update";
    string IAuditedRequest.AuditTargetType => "Brand";
    string? IAuditedRequest.AuditTargetId => Id.ToString();
    object? IAuditedRequest.AuditDetails => new { name = Name };
}
