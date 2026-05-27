using Application.Features.Orders.CreateOrder;
using MediatR;

namespace Orders.Endpoints;

public static class CreateOrderEndpoint
{
    public static IEndpointRouteBuilder MapCreateOrder(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", async (
            CreateOrderRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new CreateOrderCommand(
                request.UserId,
                request.Currency,
                request.Notes,
                new ShippingAddressRequest(
                    request.ShippingAddress.Street,
                    request.ShippingAddress.City,
                    request.ShippingAddress.State,
                    request.ShippingAddress.Country,
                    request.ShippingAddress.ZipCode,
                    request.ShippingAddress.FullName,
                    request.ShippingAddress.Phone),
                request.Items.Select(i => new OrderItemRequest(
                    i.ProductId,
                    i.ProductName,
                    i.UnitPrice,
                    i.Quantity,
                    i.Currency,
                    i.ProductImageUrl)).ToList());

            var orderId = await mediator.Send(command, ct);

            return Results.Created($"/orders/{orderId}", new { orderId });
        });

        return app;
    }
}

public record CreateOrderRequest(
    Guid UserId,
    string Currency,
    string? Notes,
    ShippingAddressDto ShippingAddress,
    List<OrderItemDto> Items);

public record ShippingAddressDto(
    string Street,
    string City,
    string State,
    string Country,
    string ZipCode,
    string? FullName,
    string? Phone);

public record OrderItemDto(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    string Currency,
    string? ProductImageUrl);
