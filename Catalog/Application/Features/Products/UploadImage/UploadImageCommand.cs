using Application.DTOs;
using System.Text.Json.Serialization;
using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Products.UploadImage;

/// <summary>
/// Adds an image to a product. <see cref="Content"/> must be seekable (uploaded form files are); its type is
/// detected from the bytes, so <see cref="ClaimedContentType"/> is only recorded for diagnostics.
/// </summary>
public record UploadProductImageCommand(
    Guid ProductId,
    [property: JsonIgnore] Stream Content,
    long Length,
    string FileName,
    string? ClaimedContentType,
    bool IsPrimary
) : IRequest<UploadProductImageDto>, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "product.image.upload";
    string IAuditedRequest.AuditTargetType => "Product";
    string? IAuditedRequest.AuditTargetId => ProductId.ToString();
    object? IAuditedRequest.AuditDetails => new { fileName = FileName, sizeBytes = Length, isPrimary = IsPrimary };
}
