using Domain.Enums;
using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Products.SetStatus;

public record SetProductStatusCommand(Guid ProductId, ProductStatus Status)
    : IRequest<SetProductStatusResult>, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "product.status.change";
    string IAuditedRequest.AuditTargetType => "Product";
    string? IAuditedRequest.AuditTargetId => ProductId.ToString();
    object? IAuditedRequest.AuditDetails => new { status = Status.ToString() };
}

public record SetProductStatusResult(Guid ProductId, string PreviousStatus, string Status);
