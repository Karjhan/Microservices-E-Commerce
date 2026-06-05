using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Contracts.Events;
using Domain.Entities;
using MediatR;

namespace Application.Features.Orders.CreateOrder;

public class CreateOrderHandler(
    IOrderRepository repo,
    IEventPublisher publisher,
    ICacheService cache)
    : IRequestHandler<CreateOrderCommand, Guid>
{
    public async Task<Guid> Handle(CreateOrderCommand request, CancellationToken ct)
    {
        if (request.Items.Count == 0)
        {
            throw new ArgumentException("An order must contain at least one item.");
        }
        
        var order = new Order(
            request.UserId,
            request.Currency,
            request.Notes);

        var address = new ShippingAddress(
            order.Id,
            request.ShippingAddress.Street,
            request.ShippingAddress.City,
            request.ShippingAddress.State,
            request.ShippingAddress.Country,
            request.ShippingAddress.ZipCode,
            request.ShippingAddress.FullName,
            request.ShippingAddress.Phone);

        order.SetShippingAddress(address);

        foreach (var item in request.Items)
        {
            var orderItem = new OrderItem(
                order.Id,
                item.ProductId,
                item.ProductName,
                item.UnitPrice,
                item.Quantity,
                item.Currency,
                item.ProductImageUrl);

            await repo.AddItemAsync(orderItem, ct);
            order.AddItem(orderItem);
        }

        await repo.AddAsync(order, ct);
        await repo.SaveChangesAsync(ct);

        await cache.RemoveByPrefixAsync("orders:");

        await publisher.PublishAsync(new OrderCreated
        {
            OrderId = order.Id,
            UserId = order.UserId,
            TotalAmount = order.TotalAmount,
            Currency = order.Currency,
            ItemCount = order.Items.Count,
            CreatedAt = order.CreatedAt,
            Items = order.Items
                .Select(i => new OrderCreatedLineItem(i.ProductId, i.Quantity))
                .ToList()
        }, "order.created");

        return order.Id;
    }
}
