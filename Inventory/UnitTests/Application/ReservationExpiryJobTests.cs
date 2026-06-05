using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Features.Inventory.ReleaseStock;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UnitTests.Helpers;

namespace UnitTests.Application;

/// <summary>
/// Verifies the selection contract that the expiry job depends on:
/// repository must return Active reservations whose ExpiresAt &lt;= asOf,
/// and ReleaseStockHandler must skip terminal reservations as a no-op.
/// </summary>
public class ReservationExpiryJobTests
{
    [Fact]
    public async Task ReleaseHandler_Should_NoOp_For_Terminal_Reservations_Only()
    {
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var expired = new StockReservation(orderId, productId, 1, DateTime.UtcNow.AddMinutes(-5));
        expired.Expire();

        var deducted = new StockReservation(orderId, productId, 2, DateTime.UtcNow.AddMinutes(-5));
        deducted.Deduct();

        var repo = new Mock<IInventoryRepository>();
        var publisher = new Mock<IEventPublisher>();
        var cache = new Mock<ICacheService>();

        repo.Setup(x => x.GetReservationsByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockReservation> { expired, deducted });

        repo.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new FakeTransactionScope());

        var handler = new ReleaseStockHandler(
            repo.Object,
            publisher.Object,
            cache.Object,
            NullLogger<ReleaseStockHandler>.Instance);

        var result = await handler.Handle(
            new ReleaseStockCommand(orderId, "reservation.expired", null, MarkExpired: true),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ReleasedCount.Should().Be(0);

        repo.Verify(
            x => x.AddMovementAsync(It.IsAny<InventoryMovement>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReleaseHandler_Should_Mark_Active_Reservations_As_Expired()
    {
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var item = new InventoryItem(productId, 10);
        item.Reserve(3);

        var active = new StockReservation(orderId, productId, 3, DateTime.UtcNow.AddMinutes(-1));

        var repo = new Mock<IInventoryRepository>();
        var publisher = new Mock<IEventPublisher>();
        var cache = new Mock<ICacheService>();

        repo.Setup(x => x.GetReservationsByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockReservation> { active });

        repo.Setup(x => x.GetByProductIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InventoryItem> { item });

        repo.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new FakeTransactionScope());

        repo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        repo.Setup(x => x.AddMovementAsync(It.IsAny<InventoryMovement>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        publisher.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        cache.Setup(x => x.RemoveAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        cache.Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        var handler = new ReleaseStockHandler(
            repo.Object,
            publisher.Object,
            cache.Object,
            NullLogger<ReleaseStockHandler>.Instance);

        var result = await handler.Handle(
            new ReleaseStockCommand(orderId, "reservation.expired", null, MarkExpired: true),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ReleasedCount.Should().Be(1);
        active.Status.Should().Be(ReservationStatus.Expired);
        item.ReservedQuantity.Should().Be(0);
    }
}
