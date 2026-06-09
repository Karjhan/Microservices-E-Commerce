using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Features.Orders.CreateOrder;
using Contracts.Events;
using Domain.Entities;
using FluentAssertions;
using Moq;

namespace UnitTests.Application;

public class CreateOrderHandlerTests
{
    private readonly Mock<IOrderRepository> _repo = new();
    private readonly Mock<IEventPublisher> _publisher = new();
    private readonly Mock<ICacheService> _cache = new();

    private readonly CreateOrderHandler _handler;

    public CreateOrderHandlerTests()
    {
        _handler = new CreateOrderHandler(_repo.Object, _publisher.Object, _cache.Object);
    }

    [Fact]
    public async Task Should_Create_Order_And_Return_Id()
    {
        // Arrange
        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            "EUR",
            "Notes",
            new ShippingAddressRequest("Street", "City", "State", "Country", "12345", null, null),
            new List<OrderItemRequest>
            {
                new(Guid.NewGuid(), "Headphones", 49.99m, 2, "EUR", null)
            });

        _repo.Setup(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(x => x.AddItemAsync(It.IsAny<OrderItem>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _cache.Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _publisher.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        _repo.Verify(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(x => x.AddItemAsync(It.IsAny<OrderItem>(), It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Should_Create_Multiple_Items()
    {
        // Arrange
        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            "EUR",
            null,
            new ShippingAddressRequest("Street", "City", "State", "Country", "12345", null, null),
            new List<OrderItemRequest>
            {
                new(Guid.NewGuid(), "Item A", 10m, 1, "EUR", null),
                new(Guid.NewGuid(), "Item B", 20m, 2, "EUR", null),
                new(Guid.NewGuid(), "Item C", 30m, 3, "EUR", null)
            });

        _repo.Setup(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(x => x.AddItemAsync(It.IsAny<OrderItem>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _cache.Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _publisher.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _repo.Verify(x => x.AddItemAsync(It.IsAny<OrderItem>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Should_Publish_OrderCreated_Event()
    {
        // Arrange
        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            "USD",
            null,
            new ShippingAddressRequest("St", "City", "State", "Country", "00000", null, null),
            new List<OrderItemRequest>
            {
                new(Guid.NewGuid(), "Product", 25m, 1, "USD", null)
            },
            RecipientEmail:"customer@example.com");

        _repo.Setup(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(x => x.AddItemAsync(It.IsAny<OrderItem>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _cache.Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _publisher.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _publisher.Verify(
            x => x.PublishAsync(It.IsAny<OrderCreated>(), "order.created"),
            Times.Once);
    }

    [Fact]
    public async Task Should_Publish_OrderCreated_With_Line_Items()
    {
        // Arrange
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();

        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            "EUR",
            null,
            new ShippingAddressRequest("St", "City", "State", "Country", "00000", null, null),
            new List<OrderItemRequest>
            {
                new(productA, "Headphones", 49.99m, 2, "EUR", null),
                new(productB, "Cable",       9.99m, 3, "EUR", null)
            },
            RecipientEmail:"customer@example.com");

        _repo.Setup(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(x => x.AddItemAsync(It.IsAny<OrderItem>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _cache.Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        OrderCreated? publishedEvent = null;
        _publisher
            .Setup(x => x.PublishAsync(It.IsAny<OrderCreated>(), "order.created"))
            .Callback<OrderCreated, string>((evt, _) => publishedEvent = evt)
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        publishedEvent.Should().NotBeNull();
        publishedEvent!.Items.Should().HaveCount(2);
        publishedEvent.Items.Should().Contain(i => i.ProductId == productA && i.Quantity == 2);
        publishedEvent.Items.Should().Contain(i => i.ProductId == productB && i.Quantity == 3);
    }

    [Fact]
    public async Task Should_Invalidate_Cache()
    {
        // Arrange
        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            "EUR",
            null,
            new ShippingAddressRequest("St", "City", "State", "Country", "00000", null, null),
            new List<OrderItemRequest>
            {
                new(Guid.NewGuid(), "Product", 10m, 1, "EUR", null)
            });

        _repo.Setup(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(x => x.AddItemAsync(It.IsAny<OrderItem>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _cache.Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _publisher.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _cache.Verify(x => x.RemoveByPrefixAsync("orders:"), Times.Once);
    }
}