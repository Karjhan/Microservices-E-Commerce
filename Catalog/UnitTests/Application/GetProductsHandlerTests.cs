using Application.DTOs;
using Application.Features.Products.GetProducts;
using FluentAssertions;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using Moq;
using UnitTests.Helpers;

namespace UnitTests.Application;

public class GetProductsHandlerTests
{
    private readonly Mock<IProductRepository> _repo = new();
    private readonly Mock<ICacheService> _cache = new();

    private readonly GetProductsHandler _handler;

    public GetProductsHandlerTests()
    {
        _handler = new GetProductsHandler(_repo.Object, _cache.Object);
    }

    [Fact]
    public async Task Should_Return_From_Cache()
    {
        var list = new List<ProductDto>
        {
            ProductDto.FromEntity(TestDataBuilder.CreateProduct())
        };

        _cache.Setup(x => x.GetAsync<List<ProductDto>>(It.IsAny<string>()))
            .ReturnsAsync(list);

        var result = await _handler.Handle(
            new GetProductsQuery(new Domain.Commons.ProductFilter()),
            CancellationToken.None);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task Should_Query_Repository_When_No_Cache()
    {
        _cache.Setup(x => x.GetAsync<List<ProductDto>>(It.IsAny<string>()))
            .ReturnsAsync((List<ProductDto>?)null);

        _repo.Setup(x => x.GetFilteredAsync(It.IsAny<Domain.Commons.ProductFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Domain.Entities.Product>());

        _cache.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()))
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(
            new GetProductsQuery(new Domain.Commons.ProductFilter()),
            CancellationToken.None);

        result.Should().BeEmpty();
    }
}