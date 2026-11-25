using Application.Abstractions.Caching;
using Application.Abstractions.Persistence;
using Application.DTOs;
using Application.Features.Orders.GetOrderById;
using Domain.Entities;
using Domain.Exceptions;
using FluentAssertions;
using Moq;
using UnitTests.Helpers;

namespace UnitTests.Application;

public class GetOrderByIdHandlerTests
{
    private readonly Mock<IOrderRepository> _repo = new();
    private readonly Mock<ICacheService> _cache = new();

    private readonly GetOrderByIdHandler _handler;

    public GetOrderByIdHandlerTests()
    {
        _handler = new GetOrderByIdHandler(_repo.Object, _cache.Object, FakeCurrentUser.Admin());
    }

    [Fact]
    public async Task Should_Return_Cached_Order_When_Available()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var cachedDto = new OrderDto
        {
            Id = orderId,
            UserId = Guid.NewGuid(),
            Status = "PendingPayment",
            Currency = "EUR"
        };

        _cache.Setup(x => x.GetAsync<OrderDto>($"order:{orderId}"))
            .ReturnsAsync(cachedDto);

        // Act
        var result = await _handler.Handle(new GetOrderByIdQuery(orderId), CancellationToken.None);

        // Assert
        result.Should().Be(cachedDto);
        _repo.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Should_Fetch_From_Repo_When_Not_Cached()
    {
        // Arrange
        var order = TestDataBuilder.CreateFullOrder();

        _cache.Setup(x => x.GetAsync<OrderDto>($"order:{order.Id}"))
            .ReturnsAsync((OrderDto?)null);
        _cache.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<OrderDto>(), It.IsAny<TimeSpan?>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        var result = await _handler.Handle(new GetOrderByIdQuery(order.Id), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(order.Id);
        _cache.Verify(x => x.SetAsync($"order:{order.Id}", It.IsAny<OrderDto>(), It.IsAny<TimeSpan?>()), Times.Once);
    }

    [Fact]
    public async Task Should_Throw_When_Order_Not_Found()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        _cache.Setup(x => x.GetAsync<OrderDto>($"order:{orderId}"))
            .ReturnsAsync((OrderDto?)null);
        _repo.Setup(x => x.GetByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        // Act & Assert
        await Assert.ThrowsAsync<OrderNotFoundException>(
            () => _handler.Handle(new GetOrderByIdQuery(orderId), CancellationToken.None));
    }

    [Fact]
    public async Task Should_Return_Order_To_Its_Owner()
    {
        var order = TestDataBuilder.CreateFullOrder();
        _repo.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _cache.Setup(x => x.GetAsync<OrderDto>($"order:{order.Id}")).ReturnsAsync((OrderDto?)null);

        var handler = new GetOrderByIdHandler(_repo.Object, _cache.Object, FakeCurrentUser.Customer(order.UserId));

        var result = await handler.Handle(new GetOrderByIdQuery(order.Id), CancellationToken.None);

        result.Id.Should().Be(order.Id);
    }

    [Fact]
    public async Task Should_Report_Not_Found_For_Non_Owner_When_Loaded_From_Database()
    {
        var order = TestDataBuilder.CreateFullOrder();
        _repo.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _cache.Setup(x => x.GetAsync<OrderDto>($"order:{order.Id}")).ReturnsAsync((OrderDto?)null);

        var handler = new GetOrderByIdHandler(_repo.Object, _cache.Object, FakeCurrentUser.Customer(Guid.NewGuid()));

        await Assert.ThrowsAsync<OrderNotFoundException>(
            () => handler.Handle(new GetOrderByIdQuery(order.Id), CancellationToken.None));

        _cache.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<OrderDto>(), It.IsAny<TimeSpan?>()), Times.Never);
    }

    [Fact]
    public async Task Should_Report_Not_Found_For_Non_Owner_When_Served_From_Cache()
    {
        var orderId = Guid.NewGuid();
        var cached = new OrderDto { Id = orderId, UserId = Guid.NewGuid(), Status = "PendingPayment", Currency = "EUR" };
        _cache.Setup(x => x.GetAsync<OrderDto>($"order:{orderId}")).ReturnsAsync(cached);

        var handler = new GetOrderByIdHandler(_repo.Object, _cache.Object, FakeCurrentUser.Customer(Guid.NewGuid()));

        await Assert.ThrowsAsync<OrderNotFoundException>(
            () => handler.Handle(new GetOrderByIdQuery(orderId), CancellationToken.None));
    }
}
