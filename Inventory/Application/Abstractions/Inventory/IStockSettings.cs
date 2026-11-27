namespace Application.Abstractions.Inventory;

public interface IStockSettings
{
    /// <summary>The low-stock threshold a product gets when it has no stock record yet (FR-028).</summary>
    int DefaultLowStockThreshold { get; }
}
