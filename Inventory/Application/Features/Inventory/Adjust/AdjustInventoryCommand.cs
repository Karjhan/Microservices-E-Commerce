using Application.DTOs;
using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Inventory.Adjust;

public record AdjustInventoryCommand(
    Guid ProductId,
    int Delta,
    string Reason
) : IRequest<InventoryItemDto>, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "inventory.adjust";
    string IAuditedRequest.AuditTargetType => "InventoryItem";
    string? IAuditedRequest.AuditTargetId => ProductId.ToString();
    object? IAuditedRequest.AuditDetails => new { delta = Delta, reason = Reason };
}