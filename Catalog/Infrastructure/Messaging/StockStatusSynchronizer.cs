using Application.Abstractions.Messaging;
using Contracts.Events;
using Domain.Enums;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Messaging;

/// <summary>
/// Keeps a product's status in step with its stock: <c>Active</c> becomes <c>OutOfStock</c> when nothing is
/// available, and <c>OutOfStock</c> becomes <c>Active</c> again on restock (data-model.md, product status).
/// Safe to run twice for the same event: the second run finds nothing to change.
/// </summary>
public class StockStatusSynchronizer(
    IProductRepository repo,
    IEventPublisher publisher,
    ILogger<StockStatusSynchronizer> logger,
    ICacheService? cache = null)
{
    /// <returns>True when the status changed.</returns>
    public async Task<bool> ApplyAsync(Guid productId, int availableQuantity, CancellationToken ct)
    {
        var product = await repo.GetByIdAsync(productId, ct);

        if (product is null)
        {
            // The stock exists before the product does when stock is set during creation, or the product was deleted.
            logger.LogDebug("Stock update for unknown product {ProductId} ignored", productId);
            return false;
        }

        var target = ProductStatusRules.StatusAfterStockChange(product.Status, availableQuantity);
        if (target is null)
            return false;

        var previous = product.Status;
        if (!product.ChangeStatus(target.Value))
            return false;

        await repo.SaveChangesAsync(ct);

        if (cache is not null)
        {
            await cache.RemoveAsync($"product:{product.Id}");
            await cache.RemoveByPrefixAsync("products:");
        }

        await publisher.PublishAsync(new ProductStatusChanged
        {
            ProductId = product.Id,
            PreviousStatus = previous.ToString(),
            Status = product.Status.ToString(),
            IsVisible = product.Status == ProductStatus.Active,
            ChangedAt = product.UpdatedAt ?? DateTime.UtcNow
        }, "product.status.changed");

        logger.LogInformation(
            "Product {ProductId} moved from {Previous} to {Status} (available {Available})",
            product.Id, previous, product.Status, availableQuantity);

        return true;
    }
}
