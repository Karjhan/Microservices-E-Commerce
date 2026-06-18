namespace Application.Abstractions.Enrichment;

public interface IOrderEnrichmentClient
{
    Task<OrderEnrichmentDto?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
}

public class OrderEnrichmentDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public decimal TotalAmount { get; init; }
    public string Currency { get; init; } = default!;
    public string? RecipientEmail { get; init; }
    public ShippingAddressEnrichmentDto? ShippingAddress { get; init; }
    public List<OrderItemEnrichmentDto> Items { get; init; } = [];
}

public class OrderItemEnrichmentDto
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = default!;
    public decimal UnitPrice { get; init; }
    public int Quantity { get; init; }
    public string Currency { get; init; } = default!;
    public decimal LineTotal { get; init; }
}

public class ShippingAddressEnrichmentDto
{
    public string Street { get; init; } = default!;
    public string City { get; init; } = default!;
    public string? State { get; init; }
    public string Country { get; init; } = default!;
    public string ZipCode { get; init; } = default!;
    public string? FullName { get; init; }
}
