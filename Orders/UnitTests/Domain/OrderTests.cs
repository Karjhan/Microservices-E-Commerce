using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using FluentAssertions;

namespace UnitTests.Domain;

public class OrderTests
{
    [Fact]
    public void Constructor_Should_Set_Properties()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var order = new Order(userId, "EUR", "Test notes");

        // Assert
        order.Id.Should().NotBeEmpty();
        order.UserId.Should().Be(userId);
        order.Currency.Should().Be("EUR");
        order.Notes.Should().Be("Test notes");
        order.Status.Should().Be(OrderStatus.PendingPayment);
        order.TotalAmount.Should().Be(0);
        order.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        order.Items.Should().BeEmpty();
    }

    [Fact]
    public void AddItem_Should_Add_And_Recalculate_Total()
    {
        // Arrange
        var order = new Order(Guid.NewGuid(), "EUR");
        var item = new OrderItem(order.Id, Guid.NewGuid(), "Headphones", 50m, 2, "EUR");

        // Act
        order.AddItem(item);

        // Assert
        order.Items.Should().ContainSingle();
        order.TotalAmount.Should().Be(100m);
    }

    [Fact]
    public void AddItem_Should_Accumulate_Total_For_Multiple_Items()
    {
        // Arrange
        var order = new Order(Guid.NewGuid(), "EUR");
        var item1 = new OrderItem(order.Id, Guid.NewGuid(), "Item A", 10m, 1, "EUR");
        var item2 = new OrderItem(order.Id, Guid.NewGuid(), "Item B", 20m, 3, "EUR");

        // Act
        order.AddItem(item1);
        order.AddItem(item2);

        // Assert
        order.Items.Should().HaveCount(2);
        order.TotalAmount.Should().Be(70m); // 10 + 60
    }

    [Fact]
    public void SetShippingAddress_Should_Assign_Address()
    {
        // Arrange
        var order = new Order(Guid.NewGuid(), "EUR");
        var address = new ShippingAddress(order.Id, "Street", "City", "State", "Country", "12345");

        // Act
        order.SetShippingAddress(address);

        // Assert
        order.ShippingAddress.Should().Be(address);
    }

    [Fact]
    public void MarkPaid_Should_Transition_From_PendingPayment()
    {
        // Arrange
        var order = new Order(Guid.NewGuid(), "EUR");

        // Act
        order.MarkPaid();

        // Assert
        order.Status.Should().Be(OrderStatus.Paid);
        order.PaidAt.Should().NotBeNull();
        order.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkPaid_Should_Throw_When_Not_PendingPayment()
    {
        // Arrange
        var order = new Order(Guid.NewGuid(), "EUR");
        order.MarkPaid();

        // Act
        var act = () => order.MarkPaid();

        // Assert
        act.Should().Throw<InvalidOrderStateException>();
    }

    [Fact]
    public void MarkProcessing_Should_Transition_From_Paid()
    {
        // Arrange
        var order = new Order(Guid.NewGuid(), "EUR");
        order.MarkPaid();

        // Act
        order.MarkProcessing();

        // Assert
        order.Status.Should().Be(OrderStatus.Processing);
        order.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkProcessing_Should_Throw_When_Not_Paid()
    {
        // Arrange
        var order = new Order(Guid.NewGuid(), "EUR");

        // Act
        var act = () => order.MarkProcessing();

        // Assert
        act.Should().Throw<InvalidOrderStateException>();
    }

    [Fact]
    public void MarkShipped_Should_Transition_From_Processing()
    {
        // Arrange
        var order = new Order(Guid.NewGuid(), "EUR");
        order.MarkPaid();
        order.MarkProcessing();

        // Act
        order.MarkShipped();

        // Assert
        order.Status.Should().Be(OrderStatus.Shipped);
        order.ShippedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkShipped_Should_Throw_When_Not_Processing()
    {
        // Arrange
        var order = new Order(Guid.NewGuid(), "EUR");

        // Act
        var act = () => order.MarkShipped();

        // Assert
        act.Should().Throw<InvalidOrderStateException>();
    }

    [Fact]
    public void MarkDelivered_Should_Transition_From_Shipped()
    {
        // Arrange
        var order = new Order(Guid.NewGuid(), "EUR");
        order.MarkPaid();
        order.MarkProcessing();
        order.MarkShipped();

        // Act
        order.MarkDelivered();

        // Assert
        order.Status.Should().Be(OrderStatus.Delivered);
        order.DeliveredAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkDelivered_Should_Throw_When_Not_Shipped()
    {
        // Arrange
        var order = new Order(Guid.NewGuid(), "EUR");

        // Act
        var act = () => order.MarkDelivered();

        // Assert
        act.Should().Throw<InvalidOrderStateException>();
    }

    [Fact]
    public void MarkCancelled_Should_Work_From_PendingPayment()
    {
        // Arrange
        var order = new Order(Guid.NewGuid(), "EUR");

        // Act
        order.MarkCancelled("Changed my mind");

        // Assert
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancellationReason.Should().Be("Changed my mind");
        order.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkCancelled_Should_Work_From_Paid()
    {
        // Arrange
        var order = new Order(Guid.NewGuid(), "EUR");
        order.MarkPaid();

        // Act
        order.MarkCancelled("Reason");

        // Assert
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void MarkCancelled_Should_Work_From_Processing()
    {
        // Arrange
        var order = new Order(Guid.NewGuid(), "EUR");
        order.MarkPaid();
        order.MarkProcessing();

        // Act
        order.MarkCancelled("Reason");

        // Assert
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void MarkCancelled_Should_Throw_When_Shipped()
    {
        // Arrange
        var order = new Order(Guid.NewGuid(), "EUR");
        order.MarkPaid();
        order.MarkProcessing();
        order.MarkShipped();

        // Act
        var act = () => order.MarkCancelled("Reason");

        // Assert
        act.Should().Throw<InvalidOrderStateException>();
    }

    [Fact]
    public void MarkCancelled_Should_Throw_When_Delivered()
    {
        // Arrange
        var order = new Order(Guid.NewGuid(), "EUR");
        order.MarkPaid();
        order.MarkProcessing();
        order.MarkShipped();
        order.MarkDelivered();

        // Act
        var act = () => order.MarkCancelled("Reason");

        // Assert
        act.Should().Throw<InvalidOrderStateException>();
    }

    [Fact]
    public void MarkPaymentFailed_Should_Transition_From_PendingPayment()
    {
        // Arrange
        var order = new Order(Guid.NewGuid(), "EUR");

        // Act
        order.MarkPaymentFailed();

        // Assert
        order.Status.Should().Be(OrderStatus.PaymentFailed);
        order.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkPaymentFailed_Should_Throw_When_Not_PendingPayment()
    {
        // Arrange
        var order = new Order(Guid.NewGuid(), "EUR");
        order.MarkPaid();

        // Act
        var act = () => order.MarkPaymentFailed();

        // Assert
        act.Should().Throw<InvalidOrderStateException>();
    }

    [Fact]
    public void UpdateShippingAddress_Should_Work_When_PendingPayment()
    {
        // Arrange
        var order = new Order(Guid.NewGuid(), "EUR");
        var address = new ShippingAddress(order.Id, "Old St", "City", "State", "Country", "00000");
        order.SetShippingAddress(address);

        // Act
        order.UpdateShippingAddress("New St", "NewCity", "NewState", "NewCountry", "99999");

        // Assert
        order.ShippingAddress.Street.Should().Be("New St");
        order.ShippingAddress.City.Should().Be("NewCity");
        order.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateShippingAddress_Should_Work_When_Paid()
    {
        // Arrange
        var order = new Order(Guid.NewGuid(), "EUR");
        var address = new ShippingAddress(order.Id, "Old St", "City", "State", "Country", "00000");
        order.SetShippingAddress(address);
        order.MarkPaid();

        // Act
        order.UpdateShippingAddress("New St", "NewCity", "NewState", "NewCountry", "99999");

        // Assert
        order.ShippingAddress.Street.Should().Be("New St");
    }

    [Fact]
    public void UpdateShippingAddress_Should_Throw_When_Shipped()
    {
        // Arrange
        var order = new Order(Guid.NewGuid(), "EUR");
        var address = new ShippingAddress(order.Id, "Old St", "City", "State", "Country", "00000");
        order.SetShippingAddress(address);
        order.MarkPaid();
        order.MarkProcessing();
        order.MarkShipped();

        // Act
        var act = () => order.UpdateShippingAddress("New St", "City", "State", "Country", "00000");

        // Assert
        act.Should().Throw<InvalidOrderStateException>();
    }
}
