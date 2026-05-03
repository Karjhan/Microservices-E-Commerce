namespace Domain.Enums;

public enum ProductStatus
{
    Draft = 0,        // Not visible to customers
    Active = 1,       // Visible and purchasable
    Inactive = 2,     // Temporarily hidden (manual)
    OutOfStock = 3,   // No inventory (future Inventory service)
    Discontinued = 4  // Permanently removed from sale
}