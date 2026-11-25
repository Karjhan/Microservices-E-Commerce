using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Products.DeleteProduct;

public record DeleteProductCommand(Guid ProductId) : IRequest, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "product.delete";
    string IAuditedRequest.AuditTargetType => "Product";
    string? IAuditedRequest.AuditTargetId => ProductId.ToString();
    object? IAuditedRequest.AuditDetails => null;
}