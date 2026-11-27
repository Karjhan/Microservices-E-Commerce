using Application.DTOs;
using Application.Features.Products.GetProductById;
using FluentAssertions;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using Moq;
using Shared.Security.Authorization;
using UnitTests.Helpers;

namespace UnitTests.Application;

public class GetProductByIdHandlerTests
{
    private readonly Mock<IProductRepository> _repo = new();
    private readonly Mock<ICacheService> _cache = new();

    private readonly GetProductByIdHandler _handler;

    public GetProductByIdHandlerTests()
    {
        _handler = CreateHandler(FakeCurrentUser.Admin());
    }

    private GetProductByIdHandler CreateHandler(ICurrentUser user) =>
        new(_repo.Object, _cache.Object, user);

    private static Domain.Entities.Product ActiveProduct()
    {
        var product = TestDataBuilder.CreateProduct();
        product.ChangeStatus(Domain.Enums.ProductStatus.Active);
        return product;
    }

    [Fact]
    public async Task Shopper_Gets_An_Active_Product()
    {
        var product = ActiveProduct();
        _cache.Setup(x => x.GetAsync<ProductDto>(It.IsAny<string>())).ReturnsAsync((ProductDto?)null);
        _repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var result = await CreateHandler(FakeCurrentUser.Anonymous())
            .Handle(new GetProductByIdQuery(product.Id), CancellationToken.None);

        result.Id.Should().Be(product.Id);
    }

    [Fact]
    public async Task Shopper_Gets_Not_Found_For_A_Draft_Product()
    {
        var product = TestDataBuilder.CreateProduct();
        _cache.Setup(x => x.GetAsync<ProductDto>(It.IsAny<string>())).ReturnsAsync((ProductDto?)null);
        _repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var act = () => CreateHandler(FakeCurrentUser.Customer(Guid.NewGuid()))
            .Handle(new GetProductByIdQuery(product.Id), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Shopper_Gets_Not_Found_For_A_Draft_Product_Even_When_It_Is_Cached()
    {
        var product = TestDataBuilder.CreateProduct();
        _cache.Setup(x => x.GetAsync<ProductDto>($"product:{product.Id}"))
            .ReturnsAsync(ProductDto.FromEntity(product));

        var act = () => CreateHandler(FakeCurrentUser.Anonymous())
            .Handle(new GetProductByIdQuery(product.Id), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        _repo.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Admin_Gets_A_Draft_Product()
    {
        var product = TestDataBuilder.CreateProduct();
        _cache.Setup(x => x.GetAsync<ProductDto>(It.IsAny<string>())).ReturnsAsync((ProductDto?)null);
        _repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var result = await _handler.Handle(new GetProductByIdQuery(product.Id), CancellationToken.None);

        result.Status.Should().Be("Draft");
    }

    [Fact]
    public async Task Should_Return_From_Cache()
    {
        var product = TestDataBuilder.CreateProduct();
        var dto = ProductDto.FromEntity(product);

        _cache.Setup(x => x.GetAsync<ProductDto>($"product:{product.Id}"))
            .ReturnsAsync(dto);

        var result = await _handler.Handle(
            new GetProductByIdQuery(product.Id),
            CancellationToken.None);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_Load_From_Db_When_Not_Cached()
    {
        var product = TestDataBuilder.CreateProduct();

        _cache.Setup(x => x.GetAsync<ProductDto>(It.IsAny<string>()))
            .ReturnsAsync((ProductDto?)null);

        _repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _cache.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<ProductDto>(), It.IsAny<TimeSpan?>()))
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(
            new GetProductByIdQuery(product.Id),
            CancellationToken.None);

        result.Should().NotBeNull();
    }
}