using Domain.Enums;
using MediatR;

namespace Application.Features.Orders.CreateOrder;

public record CreateOrderCommand(
    Guid UserId,
    string Currency,
    string? Notes,
    ShippingAddressRequest ShippingAddress,
    List<OrderItemRequest> Items,
    string? RecipientEmail = null
) : IRequest<Guid>;

public record ShippingAddressRequest(
    string Street,
    string City,
    string State,
    string Country,
    string ZipCode,
    string? FullName,
    string? Phone);

public record OrderItemRequest(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    string Currency,
    string? ProductImageUrl);
