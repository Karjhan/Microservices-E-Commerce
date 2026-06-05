using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Features.Inventory.ReserveStock;
using Contracts.Events;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UnitTests.Helpers;

namespace UnitTests.Application;

public class ReserveStockHandlerTests
{
    private readonly Mock<IInventoryRepository> _repo = new();
    private readonly Mock<IEventPublisher> _publisher = new();
    private readonly Mock<ICacheService> _cache = new();

    private readonly ReserveStockHandler _handler;

    public ReserveStockHandlerTests()
    {
        _handler = new ReserveStockHandler(
            _repo.Object,
            _publisher.Object,
            _cache.Object,
            new FakeReservationPolicy(),
            NullLogger<ReserveStockHandler>.Instance);

        _repo.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new FakeTransactionScope());

        _repo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _repo.Setup(x => x.AddReservationAsync(It.IsAny<StockReservation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _repo.Setup(x => x.AddMovementAsync(It.IsAny<InventoryMovement>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _publisher.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _cache.Setup(x => x.RemoveAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _cache.Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Should_Reserve_All_Lines_When_Stock_Available()
    {
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        _repo.Setup(x => x.GetReservationsByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockReservation>());

        _repo.Setup(x => x.GetByProductIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InventoryItem>
            {
                new InventoryItem(productA, 10),
                new InventoryItem(productB, 5)
            });

        var result = await _handler.Handle(new ReserveStockCommand(
            orderId,
            new[]
            {
                new ReserveStockLine(productA, 2),
                new ReserveStockLine(productB, 1)
            },
            CorrelationId: null), CancellationToken.None);

        result.Success.Should().BeTrue();
        _publisher.Verify(
            x => x.PublishAsync(It.IsAny<InventoryReserved>(), "inventory.reserved"),
            Times.Once);
    }

    [Fact]
    public async Task Should_Reject_Without_Partial_Reservation_When_Any_Line_Insufficient()
    {
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        _repo.Setup(x => x.GetReservationsByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockReservation>());

        _repo.Setup(x => x.GetByProductIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InventoryItem>
            {
                new InventoryItem(productA, 10),
                new InventoryItem(productB, 0)
            });

        var result = await _handler.Handle(new ReserveStockCommand(
            orderId,
            new[]
            {
                new ReserveStockLine(productA, 2),
                new ReserveStockLine(productB, 1)
            },
            CorrelationId: null), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.UnavailableProductIds.Should().Contain(productB);

        _repo.Verify(
            x => x.AddReservationAsync(It.IsAny<StockReservation>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _publisher.Verify(
            x => x.PublishAsync(It.IsAny<InventoryReserved>(), "inventory.reserved"),
            Times.Never);
    }

    [Fact]
    public async Task Should_Treat_Duplicate_Active_Reservation_As_Idempotent()
    {
        var productA = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var existing = new StockReservation(
            orderId, productA, 2,
            DateTime.UtcNow.AddMinutes(30));

        _repo.Setup(x => x.GetReservationsByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockReservation> { existing });

        var result = await _handler.Handle(new ReserveStockCommand(
            orderId,
            new[] { new ReserveStockLine(productA, 2) },
            CorrelationId: null), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Reason.Should().Be("duplicate");

        _repo.Verify(
            x => x.AddReservationAsync(It.IsAny<StockReservation>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _publisher.Verify(
            x => x.PublishAsync(It.IsAny<InventoryReserved>(), "inventory.reserved"),
            Times.Never);
    }

    [Fact]
    public async Task Should_Emit_InventoryDepleted_When_Available_Reaches_Zero()
    {
        var productA = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        _repo.Setup(x => x.GetReservationsByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockReservation>());

        _repo.Setup(x => x.GetByProductIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InventoryItem>
            {
                new InventoryItem(productA, 2)
            });

        var result = await _handler.Handle(new ReserveStockCommand(
            orderId,
            new[] { new ReserveStockLine(productA, 2) },
            CorrelationId: null), CancellationToken.None);

        result.Success.Should().BeTrue();
        _publisher.Verify(
            x => x.PublishAsync(It.IsAny<InventoryDepleted>(), "inventory.depleted"),
            Times.Once);
    }
}
