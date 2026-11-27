using Application.Abstractions.Messaging;
using Contracts.Events;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using Infrastructure.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace UnitTests.Application;

public class StockStatusSynchronizerTests
{
    private readonly Mock<IProductRepository> _repo = new();
    private readonly Mock<IEventPublisher> _publisher = new();
    private readonly Mock<ICacheService> _cache = new();

    private StockStatusSynchronizer Sync(bool withCache = true) =>
        new(_repo.Object, _publisher.Object, NullLogger<StockStatusSynchronizer>.Instance,
            withCache ? _cache.Object : null);

    private Product ProductWith(ProductStatus status)
    {
        var product = new Product("Name", "S", "L", 10m, Guid.NewGuid());
        if (status != ProductStatus.Draft)
        {
            product.ChangeStatus(ProductStatus.Active);
            product.ChangeStatus(status);
        }

        _repo.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        return product;
    }

    [Theory]
    [InlineData(ProductStatus.Active, 0, ProductStatus.OutOfStock)]
    [InlineData(ProductStatus.Active, -2, ProductStatus.OutOfStock)]
    [InlineData(ProductStatus.OutOfStock, 1, ProductStatus.Active)]
    [InlineData(ProductStatus.Active, 3, null)]
    [InlineData(ProductStatus.OutOfStock, 0, null)]
    [InlineData(ProductStatus.Draft, 0, null)]
    [InlineData(ProductStatus.Draft, 9, null)]
    [InlineData(ProductStatus.Inactive, 0, null)]
    [InlineData(ProductStatus.Inactive, 9, null)]
    [InlineData(ProductStatus.Discontinued, 0, null)]
    [InlineData(ProductStatus.Discontinued, 9, null)]
    public void The_Rule_Only_Moves_Active_And_OutOfStock_Products(
        ProductStatus current, int available, ProductStatus? expected) =>
        ProductStatusRules.StatusAfterStockChange(current, available).Should().Be(expected);

    [Fact]
    public async Task Selling_Out_Saves_Invalidates_Caches_And_Announces_The_Change()
    {
        var product = ProductWith(ProductStatus.Active);

        var changed = await Sync().ApplyAsync(product.Id, 0, CancellationToken.None);

        changed.Should().BeTrue();
        product.Status.Should().Be(ProductStatus.OutOfStock);
        _repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.RemoveAsync($"product:{product.Id}"), Times.Once);
        _cache.Verify(c => c.RemoveByPrefixAsync("products:"), Times.Once);
        _publisher.Verify(p => p.PublishAsync(
            It.Is<ProductStatusChanged>(e => e.ProductId == product.Id
                                             && e.PreviousStatus == "Active"
                                             && e.Status == "OutOfStock"
                                             && !e.IsVisible),
            "product.status.changed"), Times.Once);
    }

    [Fact]
    public async Task Restocking_Makes_The_Product_Visible_Again()
    {
        var product = ProductWith(ProductStatus.OutOfStock);

        var changed = await Sync().ApplyAsync(product.Id, 4, CancellationToken.None);

        changed.Should().BeTrue();
        product.Status.Should().Be(ProductStatus.Active);
        _publisher.Verify(p => p.PublishAsync(
            It.Is<ProductStatusChanged>(e => e.Status == "Active" && e.IsVisible),
            "product.status.changed"), Times.Once);
    }

    [Theory]
    [InlineData(ProductStatus.Draft)]
    [InlineData(ProductStatus.Inactive)]
    [InlineData(ProductStatus.Discontinued)]
    public async Task An_Admin_Chosen_Status_Is_Left_Alone(ProductStatus status)
    {
        var product = ProductWith(status);

        var changed = await Sync().ApplyAsync(product.Id, 0, CancellationToken.None);

        changed.Should().BeFalse();
        product.Status.Should().Be(status);
        _repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _publisher.Verify(p => p.PublishAsync(It.IsAny<ProductStatusChanged>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task A_Repeated_Event_Changes_Nothing_The_Second_Time()
    {
        var product = ProductWith(ProductStatus.Active);
        var sync = Sync();

        (await sync.ApplyAsync(product.Id, 0, CancellationToken.None)).Should().BeTrue();
        (await sync.ApplyAsync(product.Id, 0, CancellationToken.None)).Should().BeFalse();

        _publisher.Verify(p => p.PublishAsync(It.IsAny<ProductStatusChanged>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task An_Unknown_Product_Is_Ignored()
    {
        var changed = await Sync().ApplyAsync(Guid.NewGuid(), 0, CancellationToken.None);

        changed.Should().BeFalse();
        _repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task It_Works_Without_A_Cache()
    {
        var product = ProductWith(ProductStatus.Active);

        (await Sync(withCache: false).ApplyAsync(product.Id, 0, CancellationToken.None)).Should().BeTrue();
    }
}
