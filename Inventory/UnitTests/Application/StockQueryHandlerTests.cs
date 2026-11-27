using Application.Abstractions.Caching;
using Application.Abstractions.Persistence;
using Application.Features.Inventory.GetByProductId;
using Application.Features.Inventory.List;
using Application.Features.Inventory.Movements;
using Application.Features.Inventory.SetThreshold;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using FluentAssertions;
using Moq;
using Shared.Security.Authorization;

namespace UnitTests.Application;

public class StockQueryHandlerTests
{
    private readonly Mock<IInventoryRepository> _repo = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly Mock<ICurrentUser> _user = new();

    public StockQueryHandlerTests()
    {
        _cache.Setup(c => c.RemoveAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _cache.Setup(c => c.RemoveByPrefixAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    // ---- GET by product: shoppers see availability only (FR-001, contracts/admin-api.md) --------------------------

    [Fact]
    public async Task A_shopper_gets_only_availability()
    {
        var item = new InventoryItem(Guid.NewGuid(), 10, lowStockThreshold: 3);
        item.Reserve(4);
        _repo.Setup(r => r.GetByProductIdAsync(item.ProductId, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        _user.SetupGet(u => u.IsAdmin).Returns(false);

        var result = await new GetInventoryByProductIdHandler(_repo.Object, _cache.Object, _user.Object)
            .Handle(new GetInventoryByProductIdQuery(item.ProductId), CancellationToken.None);

        result.Full.Should().BeNull();
        result.Body.Should().BeOfType<global::Application.DTOs.PublicInventoryDto>()
            .Which.Should().BeEquivalentTo(new { Available = 6, InStock = true });
    }

    [Fact]
    public async Task A_shopper_sees_out_of_stock_without_the_counts()
    {
        var item = new InventoryItem(Guid.NewGuid(), 3);
        item.Reserve(3);
        _repo.Setup(r => r.GetByProductIdAsync(item.ProductId, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var result = await new GetInventoryByProductIdHandler(_repo.Object, _cache.Object, _user.Object)
            .Handle(new GetInventoryByProductIdQuery(item.ProductId), CancellationToken.None);

        result.Public.Available.Should().Be(0);
        result.Public.InStock.Should().BeFalse();
    }

    [Fact]
    public async Task An_admin_gets_the_full_record()
    {
        var item = new InventoryItem(Guid.NewGuid(), 10, lowStockThreshold: 3);
        item.Reserve(4);
        _repo.Setup(r => r.GetByProductIdAsync(item.ProductId, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        _user.SetupGet(u => u.IsAdmin).Returns(true);

        var result = await new GetInventoryByProductIdHandler(_repo.Object, _cache.Object, _user.Object)
            .Handle(new GetInventoryByProductIdQuery(item.ProductId), CancellationToken.None);

        result.Full.Should().NotBeNull();
        result.Full!.OnHandQuantity.Should().Be(10);
        result.Full.ReservedQuantity.Should().Be(4);
        result.Full.LowStockThreshold.Should().Be(3);
        result.Full.Version.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task An_unknown_product_is_not_found()
    {
        _repo.Setup(r => r.GetByProductIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryItem?)null);

        var act = () => new GetInventoryByProductIdHandler(_repo.Object, _cache.Object, _user.Object)
            .Handle(new GetInventoryByProductIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<InventoryItemNotFoundException>();
    }

    // ---- list -------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_list_passes_the_filter_on_and_reports_the_threshold_and_status()
    {
        var low = new InventoryItem(Guid.NewGuid(), 2, lowStockThreshold: 5);
        var filter = new InventoryFilter { Status = StockStatus.Low, Page = 2, PageSize = 10 };
        _repo.Setup(r => r.GetFilteredAsync(filter, It.IsAny<CancellationToken>())).ReturnsAsync([low]);
        _repo.Setup(r => r.CountAsync(filter, It.IsAny<CancellationToken>())).ReturnsAsync(11);

        var result = await new ListInventoryHandler(_repo.Object, _cache.Object)
            .Handle(new ListInventoryQuery(filter), CancellationToken.None);

        result.TotalCount.Should().Be(11);
        result.TotalPages.Should().Be(2);
        var row = result.Items.Should().ContainSingle().Subject;
        row.LowStockThreshold.Should().Be(5);
        row.Status.Should().Be("low");
    }

    // ---- threshold --------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_threshold_can_be_set_and_changes_the_status()
    {
        var item = new InventoryItem(Guid.NewGuid(), 10, lowStockThreshold: 5);
        _repo.Setup(r => r.GetByProductIdAsync(item.ProductId, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var result = await new SetLowStockThresholdHandler(_repo.Object, _cache.Object)
            .Handle(new SetLowStockThresholdCommand(item.ProductId, 12), CancellationToken.None);

        result.LowStockThreshold.Should().Be(12);
        result.Status.Should().Be("low");
        _cache.Verify(c => c.RemoveAsync($"inventory:product:{item.ProductId}"), Times.Once);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(InventoryItem.MaxLowStockThreshold + 1)]
    public async Task A_threshold_out_of_range_is_a_validation_error(int threshold)
    {
        var act = () => new SetLowStockThresholdHandler(_repo.Object, _cache.Object)
            .Handle(new SetLowStockThresholdCommand(Guid.NewGuid(), threshold), CancellationToken.None);

        (await act.Should().ThrowAsync<InventoryValidationException>()).Which.Errors
            .Should().ContainKey("lowStockThreshold");
    }

    [Fact]
    public async Task A_threshold_for_a_product_without_stock_is_not_found()
    {
        _repo.Setup(r => r.GetByProductIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryItem?)null);

        var act = () => new SetLowStockThresholdHandler(_repo.Object, _cache.Object)
            .Handle(new SetLowStockThresholdCommand(Guid.NewGuid(), 3), CancellationToken.None);

        await act.Should().ThrowAsync<InventoryItemNotFoundException>();
    }

    // ---- history ----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_history_is_paged_and_shows_who_and_why()
    {
        var item = new InventoryItem(Guid.NewGuid(), 10);
        var actor = Guid.NewGuid();
        var movement = new InventoryMovement(
            item.ProductId, MovementType.Adjust, 5, 15, 0, "manual.adjust",
            reason: "Supplier delivery", actorId: actor, actorEmail: "admin@store.local");

        _repo.Setup(r => r.GetByProductIdAsync(item.ProductId, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        _repo.Setup(r => r.GetMovementsAsync(item.ProductId, 1, 20, It.IsAny<CancellationToken>())).ReturnsAsync([movement]);
        _repo.Setup(r => r.CountMovementsAsync(item.ProductId, It.IsAny<CancellationToken>())).ReturnsAsync(41);

        var result = await new GetInventoryMovementsHandler(_repo.Object)
            .Handle(new GetInventoryMovementsQuery(item.ProductId), CancellationToken.None);

        result.TotalCount.Should().Be(41);
        result.TotalPages.Should().Be(3);
        var line = result.Items.Should().ContainSingle().Subject;
        line.Delta.Should().Be(5);
        line.ResultingOnHand.Should().Be(15);
        line.Reason.Should().Be("Supplier delivery");
        line.ActorId.Should().Be(actor);
        line.Actor.Should().Be("admin@store.local");
    }

    [Fact]
    public async Task The_history_page_size_is_limited()
    {
        var item = new InventoryItem(Guid.NewGuid(), 10);
        _repo.Setup(r => r.GetByProductIdAsync(item.ProductId, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        _repo.Setup(r => r.GetMovementsAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await new GetInventoryMovementsHandler(_repo.Object)
            .Handle(new GetInventoryMovementsQuery(item.ProductId, Page: 0, PageSize: 5000), CancellationToken.None);

        _repo.Verify(r => r.GetMovementsAsync(item.ProductId, 1, GetInventoryMovementsHandler.MaxPageSize, It.IsAny<CancellationToken>()), Times.Once);
    }
}
