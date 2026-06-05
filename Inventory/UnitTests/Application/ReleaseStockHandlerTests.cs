using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Features.Inventory.ReleaseStock;
using Contracts.Events;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UnitTests.Helpers;

namespace UnitTests.Application;

public class ReleaseStockHandlerTests
{
    private readonly Mock<IInventoryRepository> _repo = new();
    private readonly Mock<IEventPublisher> _publisher = new();
    private readonly Mock<ICacheService> _cache = new();

    private readonly ReleaseStockHandler _handler;

    public ReleaseStockHandlerTests()
    {
        _handler = new ReleaseStockHandler(
            _repo.Object,
            _publisher.Object,
            _cache.Object,
            NullLogger<ReleaseStockHandler>.Instance);

        _repo.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new FakeTransactionScope());

        _repo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _repo.Setup(x => x.AddMovementAsync(It.IsAny<InventoryMovement>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _publisher.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _cache.Setup(x => x.RemoveAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _cache.Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Should_Release_Active_Reservation_And_Emit_Events()
    {
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var item = new InventoryItem(productId, 10);
        item.Reserve(4);

        var reservation = new StockReservation(orderId, productId, 4, DateTime.UtcNow.AddMinutes(30));

        _repo.Setup(x => x.GetReservationsByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockReservation> { reservation });

        _repo.Setup(x => x.GetByProductIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InventoryItem> { item });

        var result = await _handler.Handle(
            new ReleaseStockCommand(orderId, "payment.failed", CorrelationId: null),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ReleasedCount.Should().Be(1);
        item.ReservedQuantity.Should().Be(0);
        reservation.Status.Should().Be(ReservationStatus.Released);

        _publisher.Verify(
            x => x.PublishAsync(It.IsAny<InventoryReleased>(), "inventory.released"),
            Times.Once);
        _publisher.Verify(
            x => x.PublishAsync(It.IsAny<InventoryUpdated>(), "inventory.updated"),
            Times.Once);
    }

    [Fact]
    public async Task Should_Be_Idempotent_On_Duplicate_Release()
    {
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var reservation = new StockReservation(orderId, productId, 2, DateTime.UtcNow.AddMinutes(30));
        reservation.Release();

        _repo.Setup(x => x.GetReservationsByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockReservation> { reservation });

        var result = await _handler.Handle(
            new ReleaseStockCommand(orderId, "payment.failed", CorrelationId: null),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ReleasedCount.Should().Be(0);

        _publisher.Verify(
            x => x.PublishAsync(It.IsAny<InventoryReleased>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Should_Mark_Reservation_Expired_When_MarkExpired_True()
    {
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var item = new InventoryItem(productId, 10);
        item.Reserve(3);

        var reservation = new StockReservation(orderId, productId, 3, DateTime.UtcNow.AddMinutes(-1));

        _repo.Setup(x => x.GetReservationsByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockReservation> { reservation });

        _repo.Setup(x => x.GetByProductIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InventoryItem> { item });

        var result = await _handler.Handle(
            new ReleaseStockCommand(orderId, "reservation.expired", CorrelationId: null, MarkExpired: true),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        reservation.Status.Should().Be(ReservationStatus.Expired);
        item.ReservedQuantity.Should().Be(0);
    }
}
