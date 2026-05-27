using System.Text.Json.Serialization;
using Domain.Enums;

namespace IntegrationTests.Helpers;

public class OrderResponse
{
    public Guid OrderId { get; set; }
}

public class OrderDetailResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public OrderStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = default!;
    public string? Notes { get; set; }
    public List<OrderItemResponse> Items { get; set; } = new();
    public ShippingAddressResponse? ShippingAddress { get; set; }
}

public class OrderItemResponse
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = default!;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

public class ShippingAddressResponse
{
    public string Street { get; set; } = default!;
    public string City { get; set; } = default!;
    public string State { get; set; } = default!;
    public string Country { get; set; } = default!;
    public string ZipCode { get; set; } = default!;
}
