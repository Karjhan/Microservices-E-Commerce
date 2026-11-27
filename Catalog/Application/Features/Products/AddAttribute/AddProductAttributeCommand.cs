using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Products.AddAttribute;

public record AddProductAttributeCommand(
    Guid ProductId,
    string Key,
    string Value
) : IRequest<Guid>, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "product.attribute.add";
    string IAuditedRequest.AuditTargetType => "Product";
    string? IAuditedRequest.AuditTargetId => ProductId.ToString();
    object? IAuditedRequest.AuditDetails => new { key = Key };
}