using Application.DTOs;
using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Products.UploadImage;

public record UploadProductImageCommand(
    Guid ProductId,
    string FilePath,
    string FileName,
    string ContentType,
    bool IsPrimary
) : IRequest<UploadProductImageDto>, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "product.image.upload";
    string IAuditedRequest.AuditTargetType => "Product";
    string? IAuditedRequest.AuditTargetId => ProductId.ToString();
    object? IAuditedRequest.AuditDetails => new { fileName = FileName, contentType = ContentType, isPrimary = IsPrimary };
}