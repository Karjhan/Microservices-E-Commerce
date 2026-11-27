using Application.DTOs;
using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Products.SetPrimaryImage;

public record SetPrimaryImageCommand(Guid ProductId, Guid ImageId)
    : IRequest<IReadOnlyList<ProductImageDto>>, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "product.image.primary";
    string IAuditedRequest.AuditTargetType => "Product";
    string? IAuditedRequest.AuditTargetId => ProductId.ToString();
    object? IAuditedRequest.AuditDetails => new { imageId = ImageId };
}
