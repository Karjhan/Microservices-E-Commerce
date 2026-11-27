using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Brands.CreateBrand;

public record CreateBrandCommand(string Name) : IRequest<Guid>, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "brand.create";
    string IAuditedRequest.AuditTargetType => "Brand";
    string? IAuditedRequest.AuditTargetId => null; // the new id is taken from the response
    object? IAuditedRequest.AuditDetails => new { name = Name };
}
