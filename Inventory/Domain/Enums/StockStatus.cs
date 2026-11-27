namespace Domain.Enums;

/// <summary>How urgent a product's stock is: out (nothing available), low (at or under its threshold) or fine.</summary>
public enum StockStatus
{
    Ok = 0,
    Low = 1,
    Out = 2
}
