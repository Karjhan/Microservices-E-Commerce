using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Products.DeleteAttribute;

public record DeleteProductAttributeCommand(
    Guid ProductId,
    Guid AttributeId
) : IRequest, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "product.attribute.delete";
    string IAuditedRequest.AuditTargetType => "Product";
    string? IAuditedRequest.AuditTargetId => ProductId.ToString();
    object? IAuditedRequest.AuditDetails => new { attributeId = AttributeId };
}