using Application.Abstractions.Messaging;
using Application.Features.Products.DeleteAttribute;
using FluentAssertions;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using Moq;
using UnitTests.Helpers;

namespace UnitTests.Application;

public class DeleteProductAttributeHandlerTests
{
    private readonly Mock<IProductRepository> _repo = new();
    private readonly Mock<IEventPublisher> _publisher = new();
    private readonly Mock<ICacheService> _cache = new();

    private readonly DeleteProductAttributeHandler _handler;

    public DeleteProductAttributeHandlerTests()
    {
        _handler = new DeleteProductAttributeHandler(
            _repo.Object,
            _publisher.Object,
            _cache.Object);
    }

    [Fact]
    public async Task Should_Delete_Attribute_Successfully()
    {
        var product = TestDataBuilder.CreateProduct();
        var attr = new Domain.Commons.ProductAttribute(product.Id, "color", "red");

        product.AddAttribute(attr);

        _repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _repo.Setup(x => x.DeleteAttributeAsync(product.Id, attr.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _repo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _cache.Setup(x => x.RemoveAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _cache.Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        _publisher.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var cmd = new DeleteProductAttributeCommand(product.Id, attr.Id);

        await _handler.Handle(cmd, CancellationToken.None);

        product.Attributes.Should().BeEmpty();
    }
}