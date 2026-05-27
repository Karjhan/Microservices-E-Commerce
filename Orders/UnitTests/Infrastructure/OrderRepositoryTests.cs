using Application.Abstractions.Persistence;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace UnitTests.Infrastructure;

public class OrderRepositoryTests
{
    private static OrdersDbContext CreateDbContext(string? dbName = null)
    {
        var name = dbName ?? Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseSqlite($"Filename={name};Mode=Memory;Cache=Shared")
            .Options;

        var context = new OrdersDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();

        return context;
    }

    [Fact]
    public async Task AddAsync_Should_Persist_Order()
    {
        // Arrange
        var context = CreateDbContext();
        var repo = new OrderRepository(context);

        var order = new Order(Guid.NewGuid(), "EUR", "Test");
        var address = new ShippingAddress(order.Id, "St", "City", "State", "Country", "00000");
        order.SetShippingAddress(address);

        // Act
        await repo.AddAsync(order, default);
        await repo.SaveChangesAsync(default);

        // Assert
        context.Orders.Count().Should().Be(1);
    }

    [Fact]
    public async Task GetByIdAsync_Should_Return_Order_With_Items_And_Address()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var writeContext = CreateDbContext(dbName);
        var writeRepo = new OrderRepository(writeContext);

        var order = new Order(Guid.NewGuid(), "EUR");
        var address = new ShippingAddress(order.Id, "Street", "City", "State", "Country", "12345");
        order.SetShippingAddress(address);

        var item = new OrderItem(order.Id, Guid.NewGuid(), "Product A", 25m, 2, "EUR");
        order.AddItem(item);

        await writeRepo.AddAsync(order, default);
        await writeRepo.AddItemAsync(item, default);
        await writeRepo.SaveChangesAsync(default);

        // Act
        var readContext = CreateDbContext(dbName);
        var readRepo = new OrderRepository(readContext);
        var result = await readRepo.GetByIdAsync(order.Id, default);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
        result.ShippingAddress.Should().NotBeNull();
        result.ShippingAddress.Street.Should().Be("Street");
    }

    [Fact]
    public async Task GetByUserIdAsync_Should_Return_Orders_For_User()
    {
        // Arrange
        var context = CreateDbContext();
        var repo = new OrderRepository(context);

        var userId = Guid.NewGuid();

        var order1 = new Order(userId, "EUR");
        order1.SetShippingAddress(new ShippingAddress(order1.Id, "St1", "City", "State", "Country", "00000"));

        var order2 = new Order(userId, "USD");
        order2.SetShippingAddress(new ShippingAddress(order2.Id, "St2", "City", "State", "Country", "00000"));

        var otherOrder = new Order(Guid.NewGuid(), "EUR");
        otherOrder.SetShippingAddress(new ShippingAddress(otherOrder.Id, "St3", "City", "State", "Country", "00000"));

        await repo.AddAsync(order1, default);
        await repo.AddAsync(order2, default);
        await repo.AddAsync(otherOrder, default);
        await repo.SaveChangesAsync(default);

        // Act
        var result = await repo.GetByUserIdAsync(userId, default);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(o => o.UserId.Should().Be(userId));
    }

    [Fact]
    public async Task GetFilteredAsync_Should_Filter_By_Status()
    {
        // Arrange
        var context = CreateDbContext();
        var repo = new OrderRepository(context);

        var o1 = new Order(Guid.NewGuid(), "EUR");
        o1.SetShippingAddress(new ShippingAddress(o1.Id, "St", "City", "State", "Country", "00000"));

        var o2 = new Order(Guid.NewGuid(), "EUR");
        o2.SetShippingAddress(new ShippingAddress(o2.Id, "St", "City", "State", "Country", "00000"));
        o2.MarkPaid();

        await repo.AddAsync(o1, default);
        await repo.AddAsync(o2, default);
        await repo.SaveChangesAsync(default);

        var filter = new OrderFilter { Status = OrderStatus.Paid };

        // Act
        var result = await repo.GetFilteredAsync(filter, default);

        // Assert
        result.Should().HaveCount(1);
        result.First().Status.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public async Task GetFilteredAsync_Should_Paginate()
    {
        // Arrange
        var context = CreateDbContext();
        var repo = new OrderRepository(context);

        for (var i = 0; i < 5; i++)
        {
            var order = new Order(Guid.NewGuid(), "EUR");
            order.SetShippingAddress(new ShippingAddress(order.Id, "St", "City", "State", "Country", "00000"));
            await repo.AddAsync(order, default);
        }

        await repo.SaveChangesAsync(default);

        var filter = new OrderFilter { Page = 2, PageSize = 2 };

        // Act
        var result = await repo.GetFilteredAsync(filter, default);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetFilteredAsync_Should_Filter_By_UserId()
    {
        // Arrange
        var context = CreateDbContext();
        var repo = new OrderRepository(context);

        var targetUserId = Guid.NewGuid();

        var o1 = new Order(targetUserId, "EUR");
        o1.SetShippingAddress(new ShippingAddress(o1.Id, "St", "City", "State", "Country", "00000"));

        var o2 = new Order(Guid.NewGuid(), "EUR");
        o2.SetShippingAddress(new ShippingAddress(o2.Id, "St", "City", "State", "Country", "00000"));

        await repo.AddAsync(o1, default);
        await repo.AddAsync(o2, default);
        await repo.SaveChangesAsync(default);

        var filter = new OrderFilter { UserId = targetUserId };

        // Act
        var result = await repo.GetFilteredAsync(filter, default);

        // Assert
        result.Should().HaveCount(1);
        result.First().UserId.Should().Be(targetUserId);
    }

    [Fact]
    public async Task GetFilteredAsync_Should_Filter_By_DateRange()
    {
        // Arrange
        var context = CreateDbContext();
        var repo = new OrderRepository(context);

        var order = new Order(Guid.NewGuid(), "EUR");
        order.SetShippingAddress(new ShippingAddress(order.Id, "St", "City", "State", "Country", "00000"));

        await repo.AddAsync(order, default);
        await repo.SaveChangesAsync(default);

        var filter = new OrderFilter
        {
            FromDate = DateTime.UtcNow.AddMinutes(-1),
            ToDate = DateTime.UtcNow.AddMinutes(1)
        };

        // Act
        var result = await repo.GetFilteredAsync(filter, default);

        // Assert
        result.Should().HaveCount(1);
    }
}
