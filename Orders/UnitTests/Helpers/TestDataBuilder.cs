using Domain.Entities;
using Domain.Enums;

namespace UnitTests.Helpers;

public static class TestDataBuilder
{
    public static Order CreateOrder(
        Guid? userId = null,
        string currency = "EUR",
        string? notes = null)
    {
        return new Order(
            userId ?? Guid.NewGuid(),
            currency,
            notes);
    }

    public static OrderItem CreateOrderItem(
        Guid orderId,
        decimal unitPrice = 29.99m,
        int quantity = 1,
        string productName = "Test Product")
    {
        return new OrderItem(
            orderId,
            Guid.NewGuid(),
            productName,
            unitPrice,
            quantity,
            "EUR",
            "https://example.com/img.jpg");
    }

    public static ShippingAddress CreateShippingAddress(Guid orderId)
    {
        return new ShippingAddress(
            orderId,
            "123 Main St",
            "Bucharest",
            "Ilfov",
            "Romania",
            "010101",
            "John Doe",
            "+40712345678");
    }

    public static Order CreateFullOrder(int itemCount = 2)
    {
        var order = CreateOrder();
        var address = CreateShippingAddress(order.Id);
        order.SetShippingAddress(address);

        for (var i = 0; i < itemCount; i++)
        {
            var item = CreateOrderItem(order.Id, unitPrice: 10m * (i + 1), quantity: i + 1);
            order.AddItem(item);
        }

        return order;
    }

    public static Order CreatePaidOrder()
    {
        var order = CreateFullOrder();
        order.MarkPaid();
        return order;
    }

    public static Order CreateProcessingOrder()
    {
        var order = CreatePaidOrder();
        order.MarkProcessing();
        return order;
    }

    public static Order CreateShippedOrder()
    {
        var order = CreateProcessingOrder();
        order.MarkShipped();
        return order;
    }
}
