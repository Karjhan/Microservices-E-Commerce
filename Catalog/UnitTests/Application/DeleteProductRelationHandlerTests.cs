using Application.Abstractions.Messaging;
using Application.Features.Products.DeleteRelation;
using FluentAssertions;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using Moq;
using UnitTests.Helpers;

namespace UnitTests.Application;

public class DeleteProductRelationHandlerTests
{
    private readonly Mock<IProductRepository> _repo = new();
    private readonly Mock<IEventPublisher> _publisher = new();
    private readonly Mock<ICacheService> _cache = new();

    private readonly DeleteProductRelationHandler _handler;

    public DeleteProductRelationHandlerTests()
    {
        _handler = new DeleteProductRelationHandler(
            _repo.Object,
            _publisher.Object,
            _cache.Object);
    }

    [Fact]
    public async Task Should_Delete_Relation()
    {
        var product = TestDataBuilder.CreateProduct();
        var relatedId = Guid.NewGuid();

        var relation = new Domain.Commons.ProductRelation(product.Id, relatedId, "similar");

        product.AddRelatedProduct(relation);

        _repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _repo.Setup(x => x.DeleteRelationAsync(product.Id, relation.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _repo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _cache.Setup(x => x.RemoveAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _cache.Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        _publisher.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _handler.Handle(
            new DeleteProductRelationCommand(product.Id, relation.Id),
            CancellationToken.None);

        product.RelatedProducts.Should().BeEmpty();
    }
}