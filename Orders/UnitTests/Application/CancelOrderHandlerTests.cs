using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Features.Orders.CancelOrder;
using Contracts.Events;
using Domain.Entities;
using Domain.Exceptions;
using FluentAssertions;
using Moq;
using UnitTests.Helpers;

namespace UnitTests.Application;

public class CancelOrderHandlerTests
{
    private readonly Mock<IOrderRepository> _repo = new();
    private readonly Mock<IEventPublisher> _publisher = new();
    private readonly Mock<ICacheService> _cache = new();

    private readonly CancelOrderHandler _handler;

    public CancelOrderHandlerTests()
    {
        _handler = new CancelOrderHandler(_repo.Object, _publisher.Object, _cache.Object);
    }

    [Fact]
    public async Task Should_Cancel_Order_In_PendingPayment_State()
    {
        // Arrange
        var order = TestDataBuilder.CreateFullOrder();

        _repo.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _repo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _cache.Setup(x => x.RemoveAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _cache.Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _publisher.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var command = new CancelOrderCommand(order.Id, "Changed mind");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        order.Status.Should().Be(global::Domain.Enums.OrderStatus.Cancelled);
        order.CancellationReason.Should().Be("Changed mind");
        _publisher.Verify(x => x.PublishAsync(It.IsAny<OrderCancelled>(), "order.cancelled"), Times.Once);
    }

    [Fact]
    public async Task Should_Throw_When_Order_Not_Found()
    {
        // Arrange
        _repo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var command = new CancelOrderCommand(Guid.NewGuid(), "Reason");

        // Act & Assert
        await Assert.ThrowsAsync<OrderNotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Should_Throw_When_Order_Already_Shipped()
    {
        // Arrange
        var order = TestDataBuilder.CreateShippedOrder();

        _repo.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new CancelOrderCommand(order.Id, "Reason");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOrderStateException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Should_Invalidate_Cache()
    {
        // Arrange
        var order = TestDataBuilder.CreateFullOrder();

        _repo.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _repo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _cache.Setup(x => x.RemoveAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _cache.Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _publisher.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var command = new CancelOrderCommand(order.Id, "Test");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _cache.Verify(x => x.RemoveAsync($"order:{order.Id}"), Times.Once);
        _cache.Verify(x => x.RemoveByPrefixAsync("orders:"), Times.Once);
    }
}
