using System.Text.Json.Serialization;
using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Brands.UploadLogo;

/// <summary>
/// Sets the brand logo. <see cref="Content"/> must be seekable (uploaded form files are); its type is detected from
/// the bytes, so <see cref="ClaimedContentType"/> is only recorded for diagnostics.
/// </summary>
public record UploadBrandLogoCommand(
    Guid BrandId,
    [property: JsonIgnore] Stream Content,
    long Length,
    string FileName,
    string? ClaimedContentType
) : IRequest<string>, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "brand.logo.upload";
    string IAuditedRequest.AuditTargetType => "Brand";
    string? IAuditedRequest.AuditTargetId => BrandId.ToString();
    object? IAuditedRequest.AuditDetails => new { fileName = FileName, sizeBytes = Length };
}
