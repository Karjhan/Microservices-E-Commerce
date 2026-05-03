using Application.Abstractions.Messaging;
using Application.Features.Products.AddRelation;
using Domain.Commons;
using FluentAssertions;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using Moq;
using UnitTests.Helpers;

namespace UnitTests.Application;

public class AddProductRelationHandlerTests
{
    private readonly Mock<IProductRepository> _repo = new();
    private readonly Mock<IEventPublisher> _publisher = new();
    private readonly Mock<ICacheService> _cache = new();

    private readonly AddProductRelationHandler _handler;

    public AddProductRelationHandlerTests()
    {
        _handler = new AddProductRelationHandler(
            _repo.Object,
            _publisher.Object,
            _cache.Object);
    }

    [Fact]
    public async Task Should_Add_Relation()
    {
        var product = TestDataBuilder.CreateProduct();
        var related = TestDataBuilder.CreateProduct();

        _repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _repo.Setup(x => x.GetByIdAsync(related.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(related);

        _repo.Setup(x => x.AddRelationAsync(It.IsAny<ProductRelation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _repo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _cache.Setup(x => x.RemoveAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _cache.Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        _publisher.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var cmd = new AddProductRelationCommand(product.Id, related.Id, "similar");

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Should().NotBeEmpty();
        product.RelatedProducts.Should().ContainSingle();
    }
}