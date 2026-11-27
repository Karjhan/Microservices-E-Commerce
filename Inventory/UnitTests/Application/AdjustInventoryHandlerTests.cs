using Application.Abstractions.Caching;
using Application.Abstractions.Inventory;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.DTOs;
using Application.Features.Inventory.Adjust;
using Contracts.Events;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using FluentAssertions;
using Moq;
using Shared.Security.Authorization;

namespace UnitTests.Application;

public class AdjustInventoryHandlerTests
{
    private readonly Mock<IInventoryRepository> _repo = new();
    private readonly Mock<IEventPublisher> _publisher = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly Mock<IStockSettings> _settings = new();
    private readonly Mock<ICurrentUser> _user = new();

    private readonly Guid _adminId = Guid.NewGuid();
    private readonly List<InventoryMovement> _movements = [];
    private readonly AdjustInventoryHandler _handler;

    public AdjustInventoryHandlerTests()
    {
        _settings.SetupGet(s => s.DefaultLowStockThreshold).Returns(5);
        _user.SetupGet(u => u.Id).Returns(_adminId);
        _user.SetupGet(u => u.Email).Returns("admin@store.local");

        _repo.Setup(r => r.AddMovementAsync(It.IsAny<InventoryMovement>(), It.IsAny<CancellationToken>()))
            .Callback<InventoryMovement, CancellationToken>((m, _) => _movements.Add(m))
            .Returns(Task.CompletedTask);
        _repo.Setup(r => r.AddItemAsync(It.IsAny<InventoryItem>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _publisher.Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        _cache.Setup(c => c.RemoveAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _cache.Setup(c => c.RemoveByPrefixAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        _handler = new AdjustInventoryHandler(
            _repo.Object, _publisher.Object, _cache.Object, _settings.Object, _user.Object);
    }

    private InventoryItem GivenStock(int onHand, int reserved = 0)
    {
        var item = new InventoryItem(Guid.NewGuid(), onHand);
        if (reserved > 0) item.Reserve(reserved);

        _repo.Setup(r => r.GetByProductIdAsync(item.ProductId, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        return item;
    }

    private Task<InventoryItemDto> Run(
        Guid productId, AdjustMode mode, int quantity, string reason = "Counted the shelf", string? expectedVersion = null) =>
        _handler.Handle(new AdjustInventoryCommand(productId, mode, quantity, reason, expectedVersion), CancellationToken.None);

    // ---- delta mode -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Delta_adds_to_the_on_hand_count()
    {
        var item = GivenStock(onHand: 5);

        var result = await Run(item.ProductId, AdjustMode.Delta, 10);

        result.OnHandQuantity.Should().Be(15);
        result.AvailableQuantity.Should().Be(15);
    }

    [Fact]
    public async Task Delta_removes_units_when_negative()
    {
        var item = GivenStock(onHand: 10);

        var result = await Run(item.ProductId, AdjustMode.Delta, -4);

        result.OnHandQuantity.Should().Be(6);
    }

    [Fact]
    public async Task Delta_of_zero_is_a_validation_error()
    {
        var item = GivenStock(onHand: 10);

        var act = () => Run(item.ProductId, AdjustMode.Delta, 0);

        var error = (await act.Should().ThrowAsync<InventoryValidationException>()).Which;
        error.Errors.Should().ContainKey("quantity");
    }

    [Fact]
    public async Task Delta_below_zero_is_refused_with_below_zero()
    {
        var item = GivenStock(onHand: 3);

        var act = () => Run(item.ProductId, AdjustMode.Delta, -4);

        (await act.Should().ThrowAsync<InvalidAdjustmentException>()).Which.Code
            .Should().Be(InvalidAdjustmentException.BelowZeroCode);
        item.OnHandQuantity.Should().Be(3);
        _movements.Should().BeEmpty();
    }

    [Fact]
    public async Task Delta_below_reserved_is_refused_with_below_reserved()
    {
        var item = GivenStock(onHand: 10, reserved: 6);

        var act = () => Run(item.ProductId, AdjustMode.Delta, -5);

        (await act.Should().ThrowAsync<InvalidAdjustmentException>()).Which.Code
            .Should().Be(InvalidAdjustmentException.BelowReservedCode);
        item.OnHandQuantity.Should().Be(10);
        _movements.Should().BeEmpty();
    }

    [Fact]
    public async Task Delta_down_to_exactly_the_reserved_count_is_allowed()
    {
        var item = GivenStock(onHand: 10, reserved: 6);

        var result = await Run(item.ProductId, AdjustMode.Delta, -4);

        result.OnHandQuantity.Should().Be(6);
        result.AvailableQuantity.Should().Be(0);
    }

    // ---- set mode ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Set_makes_the_quantity_the_new_count_and_records_the_difference()
    {
        var item = GivenStock(onHand: 10);

        var result = await Run(item.ProductId, AdjustMode.Set, 4, "Recount after inventory");

        result.OnHandQuantity.Should().Be(4);
        _movements.Should().ContainSingle().Which.QuantityDelta.Should().Be(-6);
    }

    [Fact]
    public async Task Set_to_zero_is_allowed_when_nothing_is_reserved()
    {
        var item = GivenStock(onHand: 10);

        var result = await Run(item.ProductId, AdjustMode.Set, 0, "Sold out at a fair");

        result.OnHandQuantity.Should().Be(0);
        result.Status.Should().Be("out");
    }

    [Fact]
    public async Task Set_below_reserved_is_refused()
    {
        var item = GivenStock(onHand: 10, reserved: 6);

        var act = () => Run(item.ProductId, AdjustMode.Set, 5);

        (await act.Should().ThrowAsync<InvalidAdjustmentException>()).Which.Code
            .Should().Be(InvalidAdjustmentException.BelowReservedCode);
    }

    [Fact]
    public async Task Set_to_a_negative_count_is_refused_with_below_zero()
    {
        var item = GivenStock(onHand: 10);

        var act = () => Run(item.ProductId, AdjustMode.Set, -1);

        (await act.Should().ThrowAsync<InvalidAdjustmentException>()).Which.Code
            .Should().Be(InvalidAdjustmentException.BelowZeroCode);
    }

    // ---- reason -----------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    [InlineData("  ab  ")]
    public async Task A_reason_shorter_than_3_characters_is_refused(string reason)
    {
        var item = GivenStock(onHand: 10);

        var act = () => Run(item.ProductId, AdjustMode.Delta, 1, reason);

        (await act.Should().ThrowAsync<InventoryValidationException>()).Which.Errors
            .Should().ContainKey("reason");
        _movements.Should().BeEmpty();
    }

    [Fact]
    public async Task A_reason_longer_than_200_characters_is_refused()
    {
        var item = GivenStock(onHand: 10);

        var act = () => Run(item.ProductId, AdjustMode.Delta, 1, new string('x', 201));

        (await act.Should().ThrowAsync<InventoryValidationException>()).Which.Errors
            .Should().ContainKey("reason");
    }

    [Theory]
    [InlineData(3)]
    [InlineData(200)]
    public async Task A_reason_of_3_to_200_characters_is_accepted(int length)
    {
        var item = GivenStock(onHand: 10);

        await Run(item.ProductId, AdjustMode.Delta, 1, new string('x', length));

        _movements.Should().ContainSingle().Which.Reason.Should().HaveLength(length);
    }

    [Fact]
    public async Task The_reason_is_stored_trimmed()
    {
        var item = GivenStock(onHand: 10);

        await Run(item.ProductId, AdjustMode.Delta, 1, "  Found a box  ");

        _movements.Should().ContainSingle().Which.Reason.Should().Be("Found a box");
    }

    // ---- history ----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_movement_records_who_what_and_the_resulting_count()
    {
        var item = GivenStock(onHand: 10, reserved: 2);

        await Run(item.ProductId, AdjustMode.Delta, 5, "Supplier delivery");

        var movement = _movements.Should().ContainSingle().Subject;
        movement.ProductId.Should().Be(item.ProductId);
        movement.MovementType.Should().Be(MovementType.Adjust);
        movement.QuantityDelta.Should().Be(5);
        movement.OnHandAfter.Should().Be(15);
        movement.ReservedAfter.Should().Be(2);
        movement.ActorId.Should().Be(_adminId);
        movement.ActorEmail.Should().Be("admin@store.local");
        movement.Reason.Should().Be("Supplier delivery");
    }

    // ---- stale versions ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task An_old_version_is_refused_with_stale_version_and_nothing_changes()
    {
        var item = GivenStock(onHand: 10);
        var seenByAdmin = Convert.ToBase64String(item.RowVersion);
        item.Adjust(1); // somebody else changed the stock after the admin opened it

        var act = () => Run(item.ProductId, AdjustMode.Set, 3, expectedVersion: seenByAdmin);

        (await act.Should().ThrowAsync<ConcurrencyConflictException>()).Which.Code.Should().Be("stale_version");
        item.OnHandQuantity.Should().Be(11);
        _movements.Should().BeEmpty();
    }

    [Fact]
    public async Task The_current_version_is_accepted()
    {
        var item = GivenStock(onHand: 10);

        var result = await Run(item.ProductId, AdjustMode.Set, 3, expectedVersion: Convert.ToBase64String(item.RowVersion));

        result.OnHandQuantity.Should().Be(3);
    }

    [Fact]
    public async Task A_save_that_loses_the_race_surfaces_as_stale_version()
    {
        var item = GivenStock(onHand: 10);
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException("changed while saving"));

        var act = () => Run(item.ProductId, AdjustMode.Delta, 1);

        (await act.Should().ThrowAsync<ConcurrencyConflictException>()).Which.Code.Should().Be("stale_version");
        _publisher.Verify(p => p.PublishAsync(It.IsAny<InventoryUpdated>(), It.IsAny<string>()), Times.Never);
    }

    // ---- first stock for a product ----------------------------------------------------------------------------------

    [Fact]
    public async Task The_first_change_creates_the_stock_record_with_the_default_threshold()
    {
        var productId = Guid.NewGuid();
        _repo.Setup(r => r.GetByProductIdAsync(productId, It.IsAny<CancellationToken>())).ReturnsAsync((InventoryItem?)null);
        _settings.SetupGet(s => s.DefaultLowStockThreshold).Returns(8);

        var result = await Run(productId, AdjustMode.Set, 12, "Initial stock");

        result.OnHandQuantity.Should().Be(12);
        result.LowStockThreshold.Should().Be(8);
        _repo.Verify(r => r.AddItemAsync(It.Is<InventoryItem>(i => i.ProductId == productId), It.IsAny<CancellationToken>()), Times.Once);
        _movements.Should().ContainSingle().Which.QuantityDelta.Should().Be(12);
    }

    [Fact]
    public async Task Removing_units_from_a_product_without_stock_is_not_found()
    {
        var productId = Guid.NewGuid();
        _repo.Setup(r => r.GetByProductIdAsync(productId, It.IsAny<CancellationToken>())).ReturnsAsync((InventoryItem?)null);

        var act = () => Run(productId, AdjustMode.Delta, -1);

        await act.Should().ThrowAsync<InventoryItemNotFoundException>();
    }

    // ---- events -----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task An_adjustment_publishes_the_new_stock_and_clears_the_cache()
    {
        var item = GivenStock(onHand: 10);

        await Run(item.ProductId, AdjustMode.Delta, 5);

        _publisher.Verify(p => p.PublishAsync(
            It.Is<InventoryUpdated>(e => e.ProductId == item.ProductId && e.AvailableQuantity == 15),
            "inventory.updated"), Times.Once);
        _cache.Verify(c => c.RemoveAsync($"inventory:product:{item.ProductId}"), Times.Once);
    }

    [Fact]
    public async Task Taking_stock_to_zero_also_publishes_depleted()
    {
        var item = GivenStock(onHand: 4);

        await Run(item.ProductId, AdjustMode.Set, 0, "Written off");

        _publisher.Verify(p => p.PublishAsync(It.IsAny<InventoryDepleted>(), "inventory.depleted"), Times.Once);
    }
}
