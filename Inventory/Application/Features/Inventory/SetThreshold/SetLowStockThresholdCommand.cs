using Application.DTOs;
using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Inventory.SetThreshold;

public record SetLowStockThresholdCommand(Guid ProductId, int LowStockThreshold)
    : IRequest<InventoryItemDto>, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "inventory.threshold.set";
    string IAuditedRequest.AuditTargetType => "InventoryItem";
    string? IAuditedRequest.AuditTargetId => ProductId.ToString();
    object? IAuditedRequest.AuditDetails => new { lowStockThreshold = LowStockThreshold };
}
