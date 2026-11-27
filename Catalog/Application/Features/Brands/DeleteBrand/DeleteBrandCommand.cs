using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Brands.DeleteBrand;

public record DeleteBrandCommand(Guid Id) : IRequest, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "brand.delete";
    string IAuditedRequest.AuditTargetType => "Brand";
    string? IAuditedRequest.AuditTargetId => Id.ToString();
    object? IAuditedRequest.AuditDetails => null;
}
