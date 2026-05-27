using Domain.Entities;

namespace Application.DTOs;

public class OrderDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string Status { get; init; } = default!;
    public decimal TotalAmount { get; init; }
    public string Currency { get; init; } = default!;
    public string? Notes { get; init; }

    public ShippingAddressDto? ShippingAddress { get; init; }
    public List<OrderItemDto> Items { get; init; } = new();

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? PaidAt { get; init; }
    public DateTime? ShippedAt { get; init; }
    public DateTime? DeliveredAt { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? CancellationReason { get; init; }

    public static OrderDto FromEntity(Order order)
    {
        return new OrderDto
        {
            Id = order.Id,
            UserId = order.UserId,
            Status = order.Status.ToString(),
            TotalAmount = order.TotalAmount,
            Currency = order.Currency,
            Notes = order.Notes,
            ShippingAddress = order.ShippingAddress is not null
                ? new ShippingAddressDto
                {
                    Street = order.ShippingAddress.Street,
                    City = order.ShippingAddress.City,
                    State = order.ShippingAddress.State,
                    Country = order.ShippingAddress.Country,
                    ZipCode = order.ShippingAddress.ZipCode,
                    FullName = order.ShippingAddress.FullName,
                    Phone = order.ShippingAddress.Phone
                }
                : null,
            Items = order.Items.Select(i => new OrderItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity,
                Currency = i.Currency,
                ProductImageUrl = i.ProductImageUrl,
                LineTotal = i.LineTotal
            }).ToList(),
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt,
            PaidAt = order.PaidAt,
            ShippedAt = order.ShippedAt,
            DeliveredAt = order.DeliveredAt,
            CancelledAt = order.CancelledAt,
            CancellationReason = order.CancellationReason
        };
    }
}

public class OrderItemDto
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = default!;
    public decimal UnitPrice { get; init; }
    public int Quantity { get; init; }
    public string Currency { get; init; } = default!;
    public string? ProductImageUrl { get; init; }
    public decimal LineTotal { get; init; }
}

public class ShippingAddressDto
{
    public string Street { get; init; } = default!;
    public string City { get; init; } = default!;
    public string State { get; init; } = default!;
    public string Country { get; init; } = default!;
    public string ZipCode { get; init; } = default!;
    public string? FullName { get; init; }
    public string? Phone { get; init; }
}
