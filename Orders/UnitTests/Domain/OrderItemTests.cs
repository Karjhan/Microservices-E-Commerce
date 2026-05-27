using Domain.Entities;
using FluentAssertions;

namespace UnitTests.Domain;

public class OrderItemTests
{
    [Fact]
    public void Constructor_Should_Set_Properties()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        // Act
        var item = new OrderItem(orderId, productId, "Headphones", 49.99m, 2, "EUR", "https://img.com/a.jpg");

        // Assert
        item.Id.Should().NotBeEmpty();
        item.OrderId.Should().Be(orderId);
        item.ProductId.Should().Be(productId);
        item.ProductName.Should().Be("Headphones");
        item.UnitPrice.Should().Be(49.99m);
        item.Quantity.Should().Be(2);
        item.Currency.Should().Be("EUR");
        item.ProductImageUrl.Should().Be("https://img.com/a.jpg");
    }

    [Fact]
    public void LineTotal_Should_Be_UnitPrice_Times_Quantity()
    {
        // Arrange
        var item = new OrderItem(Guid.NewGuid(), Guid.NewGuid(), "Product", 25.50m, 3, "EUR");

        // Act & Assert
        item.LineTotal.Should().Be(76.50m);
    }

    [Fact]
    public void Constructor_Should_Throw_When_ProductName_Empty()
    {
        // Act
        var act = () => new OrderItem(Guid.NewGuid(), Guid.NewGuid(), "", 10m, 1, "EUR");

        // Assert
        act.Should().Throw<ArgumentException>();
    }
    
    [Fact]
    public void Constructor_Should_Throw_When_UnitPrice_Zero()
    {
        // Act
        var act = () => new OrderItem(Guid.NewGuid(), Guid.NewGuid(), "Product", 0m, 1, "EUR");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Should_Throw_When_UnitPrice_Negative()
    {
        // Act
        var act = () => new OrderItem(Guid.NewGuid(), Guid.NewGuid(), "Product", -5m, 1, "EUR");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Should_Throw_When_Quantity_Zero()
    {
        // Act
        var act = () => new OrderItem(Guid.NewGuid(), Guid.NewGuid(), "Product", 10m, 0, "EUR");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Should_Throw_When_Quantity_Negative()
    {
        // Act
        var act = () => new OrderItem(Guid.NewGuid(), Guid.NewGuid(), "Product", 10m, -1, "EUR");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Should_Allow_Null_ImageUrl()
    {
        // Act
        var item = new OrderItem(Guid.NewGuid(), Guid.NewGuid(), "Product", 10m, 1, "EUR", null);

        // Assert
        item.ProductImageUrl.Should().BeNull();
    }
}
