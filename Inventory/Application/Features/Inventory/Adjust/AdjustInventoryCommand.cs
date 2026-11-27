using Application.DTOs;
using MediatR;
using Shared.Security.Audit;

namespace Application.Features.Inventory.Adjust;

/// <summary>How <see cref="AdjustInventoryCommand.Quantity"/> is read.</summary>
public enum AdjustMode
{
    /// <summary>Add the quantity to the on-hand count, or remove it when negative.</summary>
    Delta = 0,

    /// <summary>The quantity is the new on-hand count.</summary>
    Set = 1
}

/// <param name="ExpectedVersion">
/// The <c>version</c> of the record the admin was looking at. When it no longer matches the change is refused with
/// 409 <c>stale_version</c> instead of overwriting someone else's work.
/// </param>
public record AdjustInventoryCommand(
    Guid ProductId,
    AdjustMode Mode,
    int Quantity,
    string Reason,
    string? ExpectedVersion = null
) : IRequest<InventoryItemDto>, IAuditedRequest
{
    string IAuditedRequest.AuditAction => "inventory.adjust";
    string IAuditedRequest.AuditTargetType => "InventoryItem";
    string? IAuditedRequest.AuditTargetId => ProductId.ToString();
    object? IAuditedRequest.AuditDetails => new { mode = Mode.ToString().ToLowerInvariant(), quantity = Quantity, reason = Reason };
}
