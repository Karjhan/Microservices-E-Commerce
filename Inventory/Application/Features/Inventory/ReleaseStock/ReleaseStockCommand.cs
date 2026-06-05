using MediatR;

namespace Application.Features.Inventory.ReleaseStock;

public record ReleaseStockCommand(
    Guid OrderId,
    string Reason,
    string? CorrelationId,
    bool MarkExpired = false
) : IRequest<ReleaseStockResult>;

public record ReleaseStockResult(bool Success, int ReleasedCount, string? Note);
