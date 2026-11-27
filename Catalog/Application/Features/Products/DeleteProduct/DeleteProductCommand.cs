using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Products.DeleteProduct;

/// <summary>Deleting is irreversible, so the caller must confirm explicitly (FR-018).</summary>
public record DeleteProductCommand(Guid ProductId, bool Confirm = false) : IRequest, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "product.delete";
    string IAuditedRequest.AuditTargetType => "Product";
    string? IAuditedRequest.AuditTargetId => ProductId.ToString();
    object? IAuditedRequest.AuditDetails => null;
}
