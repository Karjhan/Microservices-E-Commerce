using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Products.DeleteModel;

public record DeleteProductModelCommand(Guid ProductId) : IRequest, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "product.model.delete";
    string IAuditedRequest.AuditTargetType => "Product";
    string? IAuditedRequest.AuditTargetId => ProductId.ToString();
    object? IAuditedRequest.AuditDetails => null;
}
