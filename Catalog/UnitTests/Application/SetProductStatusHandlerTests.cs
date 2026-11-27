using Application.Abstractions.Messaging;
using Application.Common;
using Application.Features.Products.SetStatus;
using Contracts.Events;
using Domain.Enums;
using Domain.Exceptions;
using FluentAssertions;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UnitTests.Helpers;

namespace UnitTests.Application;

public class SetProductStatusHandlerTests
{
    private readonly Mock<IProductRepository> _repo = new();
    private readonly Mock<IEventPublisher> _publisher = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly SetProductStatusHandler _handler;

    public SetProductStatusHandlerTests()
    {
        _handler = new SetProductStatusHandler(
            _repo.Object,
            _publisher.Object,
            new ProductCacheInvalidator(_cache.Object, new AfterCommitQueue(NullLogger<AfterCommitQueue>.Instance)));
    }

    [Fact]
    public async Task Publishing_A_Draft_Activates_It_Clears_The_Cache_And_Emits_An_Event()
    {
        var product = TestDataBuilder.CreateProduct();
        _repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var result = await _handler.Handle(new SetProductStatusCommand(product.Id, ProductStatus.Active), CancellationToken.None);

        result.PreviousStatus.Should().Be("Draft");
        result.Status.Should().Be("Active");
        product.Status.Should().Be(ProductStatus.Active);
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(x => x.RemoveAsync($"product:{product.Id}"), Times.Once);
        _cache.Verify(x => x.RemoveByPrefixAsync("products:"), Times.Once);
        _publisher.Verify(x => x.PublishAsync(
            It.Is<ProductStatusChanged>(e =>
                e.ProductId == product.Id && e.PreviousStatus == "Draft" && e.Status == "Active" && e.IsVisible),
            "product.status.changed"), Times.Once);
    }

    [Fact]
    public async Task Unpublishing_Reports_The_Product_As_No_Longer_Visible()
    {
        var product = TestDataBuilder.CreateProduct();
        product.ChangeStatus(ProductStatus.Active);
        _repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        await _handler.Handle(new SetProductStatusCommand(product.Id, ProductStatus.Inactive), CancellationToken.None);

        _publisher.Verify(x => x.PublishAsync(
            It.Is<ProductStatusChanged>(e => e.Status == "Inactive" && !e.IsVisible),
            "product.status.changed"), Times.Once);
    }

    [Fact]
    public async Task The_Same_Status_Changes_Nothing_And_Emits_Nothing()
    {
        var product = TestDataBuilder.CreateProduct();
        _repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var result = await _handler.Handle(new SetProductStatusCommand(product.Id, ProductStatus.Draft), CancellationToken.None);

        result.Status.Should().Be("Draft");
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _publisher.Verify(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task A_Forbidden_Transition_Is_Refused_Without_Side_Effects()
    {
        var product = TestDataBuilder.CreateProduct();
        product.ChangeStatus(ProductStatus.Discontinued);
        _repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var act = () => _handler.Handle(new SetProductStatusCommand(product.Id, ProductStatus.Active), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _publisher.Verify(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Unknown_Product_Is_Not_Found()
    {
        _repo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Domain.Entities.Product?)null);

        var act = () => _handler.Handle(new SetProductStatusCommand(Guid.NewGuid(), ProductStatus.Active), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
