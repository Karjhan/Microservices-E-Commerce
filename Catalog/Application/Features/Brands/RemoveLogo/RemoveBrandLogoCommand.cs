using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Brands.RemoveLogo;

public record RemoveBrandLogoCommand(Guid BrandId) : IRequest, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "brand.logo.remove";
    string IAuditedRequest.AuditTargetType => "Brand";
    string? IAuditedRequest.AuditTargetId => BrandId.ToString();
    object? IAuditedRequest.AuditDetails => null;
}
