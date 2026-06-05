using Domain.Entities;
using Domain.Exceptions;
using FluentAssertions;

namespace UnitTests.Domain;

public class InventoryItemTests
{
    [Fact]
    public void Constructor_Should_Set_Defaults()
    {
        var productId = Guid.NewGuid();

        var item = new InventoryItem(productId, initialOnHand: 10);

        item.ProductId.Should().Be(productId);
        item.OnHandQuantity.Should().Be(10);
        item.ReservedQuantity.Should().Be(0);
        item.AvailableQuantity.Should().Be(10);
        item.IsDepleted.Should().BeFalse();
        item.RowVersion.Should().NotBeNull();
        item.RowVersion.Should().NotBeEmpty();
    }

    [Fact]
    public void Constructor_Should_Throw_For_Negative_Initial_OnHand()
    {
        var act = () => new InventoryItem(Guid.NewGuid(), initialOnHand: -1);

        act.Should().Throw<InvalidAdjustmentException>();
    }

    [Fact]
    public void Reserve_Should_Increase_Reserved_Quantity()
    {
        var item = new InventoryItem(Guid.NewGuid(), initialOnHand: 10);

        item.Reserve(3);

        item.ReservedQuantity.Should().Be(3);
        item.AvailableQuantity.Should().Be(7);
    }

    [Fact]
    public void Reserve_Should_Throw_When_Quantity_Not_Positive()
    {
        var item = new InventoryItem(Guid.NewGuid(), initialOnHand: 10);

        var act = () => item.Reserve(0);

        act.Should().Throw<InvalidReservationStateException>();
    }

    [Fact]
    public void Reserve_Should_Throw_When_Exceeds_Available()
    {
        var item = new InventoryItem(Guid.NewGuid(), initialOnHand: 5);

        var act = () => item.Reserve(6);

        act.Should().Throw<InsufficientStockException>();
    }

    [Fact]
    public void Concurrent_Reserve_Attempts_Should_Not_Allow_Net_Negative_Available()
    {
        var item = new InventoryItem(Guid.NewGuid(), initialOnHand: 5);

        item.Reserve(3);

        var act = () => item.Reserve(3);

        act.Should().Throw<InsufficientStockException>();
        item.ReservedQuantity.Should().Be(3);
        item.AvailableQuantity.Should().Be(2);
    }

    [Fact]
    public void Release_Should_Decrease_Reserved_Quantity()
    {
        var item = new InventoryItem(Guid.NewGuid(), initialOnHand: 10);
        item.Reserve(4);

        item.Release(3);

        item.ReservedQuantity.Should().Be(1);
        item.AvailableQuantity.Should().Be(9);
    }

    [Fact]
    public void Release_Should_Throw_When_More_Than_Reserved()
    {
        var item = new InventoryItem(Guid.NewGuid(), initialOnHand: 10);
        item.Reserve(2);

        var act = () => item.Release(3);

        act.Should().Throw<InvalidReservationStateException>();
    }

    [Fact]
    public void Deduct_Should_Reduce_OnHand_And_Reserved()
    {
        var item = new InventoryItem(Guid.NewGuid(), initialOnHand: 10);
        item.Reserve(4);

        item.Deduct(3);

        item.OnHandQuantity.Should().Be(7);
        item.ReservedQuantity.Should().Be(1);
        item.AvailableQuantity.Should().Be(6);
    }

    [Fact]
    public void Deduct_Should_Throw_When_Exceeds_Reserved()
    {
        var item = new InventoryItem(Guid.NewGuid(), initialOnHand: 10);
        item.Reserve(2);

        var act = () => item.Deduct(3);

        act.Should().Throw<InvalidReservationStateException>();
    }

    [Fact]
    public void Adjust_Should_Increase_OnHand()
    {
        var item = new InventoryItem(Guid.NewGuid(), initialOnHand: 10);

        item.Adjust(5);

        item.OnHandQuantity.Should().Be(15);
    }

    [Fact]
    public void Adjust_Should_Allow_Negative_Within_Bounds()
    {
        var item = new InventoryItem(Guid.NewGuid(), initialOnHand: 10);
        item.Reserve(2);

        item.Adjust(-3);

        item.OnHandQuantity.Should().Be(7);
        item.ReservedQuantity.Should().Be(2);
        item.AvailableQuantity.Should().Be(5);
    }

    [Fact]
    public void Adjust_Should_Throw_When_Result_Negative()
    {
        var item = new InventoryItem(Guid.NewGuid(), initialOnHand: 2);

        var act = () => item.Adjust(-5);

        act.Should().Throw<InvalidAdjustmentException>();
    }

    [Fact]
    public void Adjust_Should_Throw_When_Result_Less_Than_Reserved()
    {
        var item = new InventoryItem(Guid.NewGuid(), initialOnHand: 10);
        item.Reserve(7);

        var act = () => item.Adjust(-5);

        act.Should().Throw<InvalidAdjustmentException>();
    }

    [Fact]
    public void IsDepleted_Should_Be_True_When_Available_Is_Zero()
    {
        var item = new InventoryItem(Guid.NewGuid(), initialOnHand: 3);
        item.Reserve(3);

        item.IsDepleted.Should().BeTrue();
    }

    [Fact]
    public void RowVersion_Should_Change_On_Mutation()
    {
        var item = new InventoryItem(Guid.NewGuid(), initialOnHand: 5);
        var before = item.RowVersion.ToArray();

        item.Reserve(1);

        item.RowVersion.Should().NotEqual(before);
    }
}
