using Application.Abstractions.Inventory;
using Domain.Entities;
using Microsoft.Extensions.Options;

namespace Infrastructure.Configuration;

public class StockSettings : IStockSettings
{
    private readonly InventoryOptions _options;

    public StockSettings(IOptions<InventoryOptions> options)
    {
        _options = options.Value;
    }

    public int DefaultLowStockThreshold =>
        Math.Clamp(_options.DefaultLowStockThreshold, 0, InventoryItem.MaxLowStockThreshold);
}
