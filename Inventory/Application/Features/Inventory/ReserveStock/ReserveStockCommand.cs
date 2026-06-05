using MediatR;

namespace Application.Features.Inventory.ReserveStock;

public record ReserveStockCommand(
    Guid OrderId,
    IReadOnlyList<ReserveStockLine> Lines,
    string? CorrelationId
) : IRequest<ReserveStockResult>;

public record ReserveStockLine(Guid ProductId, int Quantity);

public record ReserveStockResult(bool Success, string? Reason, IReadOnlyList<Guid> UnavailableProductIds);
