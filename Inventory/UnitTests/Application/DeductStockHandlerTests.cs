using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Features.Inventory.DeductStock;
using Contracts.Events;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UnitTests.Helpers;

namespace UnitTests.Application;

public class DeductStockHandlerTests
{
    private readonly Mock<IInventoryRepository> _repo = new();
    private readonly Mock<IEventPublisher> _publisher = new();
    private readonly Mock<ICacheService> _cache = new();

    private readonly DeductStockHandler _handler;

    public DeductStockHandlerTests()
    {
        _handler = new DeductStockHandler(
            _repo.Object,
            _publisher.Object,
            _cache.Object,
            NullLogger<DeductStockHandler>.Instance);

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
    public async Task Should_Deduct_Stock_When_Active_Reservation_Exists()
    {
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var item = new InventoryItem(productId, 10);
        item.Reserve(3);

        var reservation = new StockReservation(orderId, productId, 3, DateTime.UtcNow.AddMinutes(30));

        _repo.Setup(x => x.GetReservationsByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockReservation> { reservation });

        _repo.Setup(x => x.GetByProductIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InventoryItem> { item });

        var result = await _handler.Handle(
            new DeductStockCommand(orderId, CorrelationId: null),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        item.OnHandQuantity.Should().Be(7);
        item.ReservedQuantity.Should().Be(0);
        reservation.Status.Should().Be(ReservationStatus.Deducted);
    }

    [Fact]
    public async Task Should_Be_Idempotent_When_Reservation_Already_Deducted()
    {
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var reservation = new StockReservation(orderId, productId, 3, DateTime.UtcNow.AddMinutes(30));
        reservation.Deduct();

        _repo.Setup(x => x.GetReservationsByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockReservation> { reservation });

        var result = await _handler.Handle(
            new DeductStockCommand(orderId, CorrelationId: null),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Reason.Should().Be("already_deducted");

        _repo.Verify(
            x => x.AddMovementAsync(It.IsAny<InventoryMovement>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Should_Report_Recoverable_Outcome_When_Reservation_Missing()
    {
        var orderId = Guid.NewGuid();

        _repo.Setup(x => x.GetReservationsByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockReservation>());

        var result = await _handler.Handle(
            new DeductStockCommand(orderId, CorrelationId: null),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Reason.Should().Be("no_reservation");

        _publisher.Verify(
            x => x.PublishAsync(It.IsAny<InventoryUpdated>(), It.IsAny<string>()),
            Times.Never);
    }
}
