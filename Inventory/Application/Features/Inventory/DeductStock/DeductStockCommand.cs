using MediatR;

namespace Application.Features.Inventory.DeductStock;

public record DeductStockCommand(
    Guid OrderId,
    string? CorrelationId
) : IRequest<DeductStockResult>;

public record DeductStockResult(bool Success, string? Reason);
