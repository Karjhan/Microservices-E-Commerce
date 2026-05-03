using Application.DTOs;
using Application.Features.Products.GetProductById;
using FluentAssertions;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using Moq;
using UnitTests.Helpers;

namespace UnitTests.Application;

public class GetProductByIdHandlerTests
{
    private readonly Mock<IProductRepository> _repo = new();
    private readonly Mock<ICacheService> _cache = new();

    private readonly GetProductByIdHandler _handler;

    public GetProductByIdHandlerTests()
    {
        _handler = new GetProductByIdHandler(
            _repo.Object,
            _cache.Object);
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