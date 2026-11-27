using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Products.AddRelation;

public record AddProductRelationCommand(
    Guid ProductId,
    Guid RelatedProductId,
    string Type
) : IRequest<Guid>, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "product.relation.add";
    string IAuditedRequest.AuditTargetType => "Product";
    string? IAuditedRequest.AuditTargetId => ProductId.ToString();
    object? IAuditedRequest.AuditDetails => new { relatedProductId = RelatedProductId, type = Type };
}