using Application.DTOs;
using System.Text.Json.Serialization;
using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Products.UploadModel;

/// <summary>Attaches or replaces the product's single 3D model (FR-014).</summary>
public record UploadProductModelCommand(
    Guid ProductId,
    [property: JsonIgnore] Stream Content,
    long Length,
    string FileName
) : IRequest<UploadProductModelResult>, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "product.model.upload";
    string IAuditedRequest.AuditTargetType => "Product";
    string? IAuditedRequest.AuditTargetId => ProductId.ToString();
    object? IAuditedRequest.AuditDetails => new { fileName = FileName, sizeBytes = Length };
}

/// <summary><see cref="Created"/> is false when an existing model was replaced.</summary>
public record UploadProductModelResult(ProductModelDto Model, bool Created);
