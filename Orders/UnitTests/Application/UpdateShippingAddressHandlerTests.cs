using Application.Abstractions.Caching;
using Application.Abstractions.Persistence;
using Application.Features.Orders.UpdateShippingAddress;
using Domain.Entities;
using Domain.Exceptions;
using FluentAssertions;
using Moq;
using UnitTests.Helpers;

namespace UnitTests.Application;

public class UpdateShippingAddressHandlerTests
{
    private readonly Mock<IOrderRepository> _repo = new();
    private readonly Mock<ICacheService> _cache = new();

    private readonly UpdateShippingAddressHandler _handler;

    public UpdateShippingAddressHandlerTests()
    {
        _handler = new UpdateShippingAddressHandler(_repo.Object, _cache.Object, FakeCurrentUser.Admin());
    }

    [Fact]
    public async Task Should_Update_Address_When_PendingPayment()
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

        var command = new UpdateShippingAddressCommand(
            order.Id, "New Street", "New City", "New State", "New Country", "99999", "Jane Doe", "+1234");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        order.ShippingAddress.Street.Should().Be("New Street");
        order.ShippingAddress.City.Should().Be("New City");
        order.ShippingAddress.Country.Should().Be("New Country");
    }

    [Fact]
    public async Task Should_Throw_When_Order_Not_Found()
    {
        // Arrange
        _repo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var command = new UpdateShippingAddressCommand(
            Guid.NewGuid(), "St", "City", "State", "Country", "00000", null, null);

        // Act & Assert
        await Assert.ThrowsAsync<OrderNotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Should_Throw_When_Order_Is_Shipped()
    {
        // Arrange
        var order = TestDataBuilder.CreateShippedOrder();

        _repo.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new UpdateShippingAddressCommand(
            order.Id, "St", "City", "State", "Country", "00000", null, null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOrderStateException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Should_Report_Not_Found_And_Change_Nothing_For_Non_Owner()
    {
        var order = TestDataBuilder.CreateFullOrder();
        var street = order.ShippingAddress!.Street;
        _repo.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var handler = new UpdateShippingAddressHandler(_repo.Object, _cache.Object, FakeCurrentUser.Customer(Guid.NewGuid()));
        var command = new UpdateShippingAddressCommand(
            order.Id, "Other Street", "City", "State", "Country", "11111", null, null);

        await Assert.ThrowsAsync<OrderNotFoundException>(() => handler.Handle(command, CancellationToken.None));

        order.ShippingAddress!.Street.Should().Be(street);
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Should_Let_Owner_Update_Their_Own_Address()
    {
        var order = TestDataBuilder.CreateFullOrder();
        _repo.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _repo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _cache.Setup(x => x.RemoveAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _cache.Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        var handler = new UpdateShippingAddressHandler(_repo.Object, _cache.Object, FakeCurrentUser.Customer(order.UserId));
        var command = new UpdateShippingAddressCommand(
            order.Id, "Mine Street", "City", "State", "Country", "22222", null, null);

        await handler.Handle(command, CancellationToken.None);

        order.ShippingAddress!.Street.Should().Be("Mine Street");
    }
}
