using Application.DTOs;
using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Products.ReorderImages;

/// <summary><see cref="ImageIds"/> must list every image of the product exactly once, in the new order.</summary>
public record ReorderImagesCommand(Guid ProductId, IReadOnlyList<Guid> ImageIds)
    : IRequest<IReadOnlyList<ProductImageDto>>, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "product.image.reorder";
    string IAuditedRequest.AuditTargetType => "Product";
    string? IAuditedRequest.AuditTargetId => ProductId.ToString();
    object? IAuditedRequest.AuditDetails => new { count = ImageIds.Count };
}
