using Application.Abstractions.Messaging;
using Application.Common;
using Contracts.Events;
using Domain.Enums;
using Infrastructure.Abstractions.Persistence;
using MediatR;

namespace Application.Features.Products.SetStatus;

public class SetProductStatusHandler(
    IProductRepository repo,
    IEventPublisher publisher,
    ProductCacheInvalidator cache)
    : IRequestHandler<SetProductStatusCommand, SetProductStatusResult>
{
    public async Task<SetProductStatusResult> Handle(SetProductStatusCommand request, CancellationToken ct)
    {
        var product = await repo.GetByIdAsync(request.ProductId, ct)
                      ?? throw new KeyNotFoundException("Product not found");

        var previous = product.Status;

        // Throws a 422 for a change the rules do not allow, for example anything out of Discontinued.
        var changed = product.ChangeStatus(request.Status);

        if (!changed)
            return new SetProductStatusResult(product.Id, previous.ToString(), previous.ToString());

        await repo.SaveChangesAsync(ct);
        await cache.InvalidateAsync(product.Id);

        await publisher.PublishAsync(new ProductStatusChanged
        {
            ProductId = product.Id,
            PreviousStatus = previous.ToString(),
            Status = product.Status.ToString(),
            IsVisible = product.Status == ProductStatus.Active,
            ChangedAt = product.UpdatedAt ?? DateTime.UtcNow
        }, "product.status.changed");

        return new SetProductStatusResult(product.Id, previous.ToString(), product.Status.ToString());
    }
}
