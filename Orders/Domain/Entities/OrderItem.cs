namespace Domain.Entities;

public class OrderItem
{
    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid ProductId { get; private set; }

    public string ProductName { get; private set; } = default!;

    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    public string Currency { get; private set; } = "EUR";

    public string? ProductImageUrl { get; private set; }

    public Order Order { get; private set; } = null!;

    private OrderItem() { }

    public OrderItem(
        Guid orderId,
        Guid productId,
        string productName,
        decimal unitPrice,
        int quantity,
        string currency,
        string? productImageUrl = null)
    {
        if (string.IsNullOrEmpty(productName))
        {
            throw new ArgumentNullException("Product name cannot be null or empty.", nameof(productName));
        }

        if (unitPrice <= 0)
        {
            throw new ArgumentOutOfRangeException("Unit price must be greater than zero.", nameof(unitPrice));
        }
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException("Quantity must be greater than zero.", nameof(quantity));
        }
        
        Id = Guid.NewGuid();
        OrderId = orderId;
        ProductId = productId;
        ProductName = productName;
        UnitPrice = unitPrice;
        Quantity = quantity;
        Currency = currency;
        ProductImageUrl = productImageUrl;
    }

    public decimal LineTotal => UnitPrice * Quantity;
}
