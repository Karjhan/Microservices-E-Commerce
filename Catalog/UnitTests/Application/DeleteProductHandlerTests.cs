using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Application.Features.Products.DeleteProduct;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using Moq;
using UnitTests.Helpers;

namespace UnitTests.Application;

public class DeleteProductHandlerTests
{
    private readonly Mock<IProductRepository> _repo = new();
    private readonly Mock<IFileStorageService> _storage = new();
    private readonly Mock<IEventPublisher> _publisher = new();
    private readonly Mock<ICacheService> _cache = new();

    private readonly DeleteProductHandler _handler;

    public DeleteProductHandlerTests()
    {
        _handler = new DeleteProductHandler(
            _repo.Object,
            _storage.Object,
            _publisher.Object,
            _cache.Object);
    }

    [Fact]
    public async Task Should_Delete_Product_And_All_Images()
    {
        var product = TestDataBuilder.CreateProduct();

        product.AddImage(new Domain.Entities.ProductImage(
            product.Id,
            "img1",
            "url1",
            false));

        _repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _repo.Setup(x => x.DeleteAsync(product, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _repo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _storage.Setup(x => x.DeleteAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _cache.Setup(x => x.RemoveAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _cache.Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        _publisher.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _handler.Handle(new DeleteProductCommand(product.Id), CancellationToken.None);

        _repo.Verify(x => x.DeleteAsync(product, It.IsAny<CancellationToken>()), Times.Once);
        _storage.Verify(x => x.DeleteAsync("img1"), Times.Once);
    }
}