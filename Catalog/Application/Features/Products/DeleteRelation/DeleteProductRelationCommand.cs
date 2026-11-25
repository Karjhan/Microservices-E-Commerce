using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Products.DeleteRelation;

public record DeleteProductRelationCommand(
    Guid ProductId,
    Guid RelationId
) : IRequest, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "product.relation.delete";
    string IAuditedRequest.AuditTargetType => "Product";
    string? IAuditedRequest.AuditTargetId => ProductId.ToString();
    object? IAuditedRequest.AuditDetails => new { relationId = RelationId };
}