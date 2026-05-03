using Application.Abstractions.Messaging;
using Application.Features.Products.AddAttribute;
using Domain.Commons;
using FluentAssertions;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using Moq;
using UnitTests.Helpers;

namespace UnitTests.Application;

public class AddProductAttributeHandlerTests
{
    private readonly Mock<IProductRepository> _repo = new();
    private readonly Mock<IEventPublisher> _publisher = new();
    private readonly Mock<ICacheService> _cache = new();

    private readonly AddProductAttributeHandler _handler;

    public AddProductAttributeHandlerTests()
    {
        _handler = new AddProductAttributeHandler(
            _repo.Object,
            _publisher.Object,
            _cache.Object);
    }

    [Fact]
    public async Task Should_Add_Attribute_When_Valid()
    {
        var product = TestDataBuilder.CreateProduct();

        _repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _repo.Setup(x => x.AddAttributeAsync(It.IsAny<ProductAttribute>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _repo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _cache.Setup(x => x.RemoveAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _cache.Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        _publisher.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var command = new AddProductAttributeCommand(product.Id, "Color", "Red");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeEmpty();
        product.Attributes.Should().ContainSingle();
    }
}