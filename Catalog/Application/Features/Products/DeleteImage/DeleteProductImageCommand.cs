using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Products.DeleteImage;

public record DeleteProductImageCommand(Guid ProductId, Guid ImageId) : IRequest, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "product.image.delete";
    string IAuditedRequest.AuditTargetType => "Product";
    string? IAuditedRequest.AuditTargetId => ProductId.ToString();
    object? IAuditedRequest.AuditDetails => new { imageId = ImageId };
}