using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Application.Features.Products.DeleteImage;
using FluentAssertions;
using Infrastructure.Abstractions.Persistence;
using Moq;
using UnitTests.Helpers;

namespace UnitTests.Application;

public class DeleteProductImageHandlerTests
{
    private readonly Mock<IProductRepository> _repo = new();
    private readonly Mock<IFileStorageService> _storage = new();
    private readonly Mock<IEventPublisher> _publisher = new();

    private readonly DeleteProductImageHandler _handler;

    public DeleteProductImageHandlerTests()
    {
        _handler = new DeleteProductImageHandler(
            _repo.Object,
            _storage.Object,
            _publisher.Object);
    }

    [Fact]
    public async Task Should_Delete_Image_And_Storage_Object()
    {
        var product = TestDataBuilder.CreateProduct();

        var image = new Domain.Entities.ProductImage(
            product.Id,
            "object-key",
            "url",
            false);

        product.AddImage(image);

        _repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _storage.Setup(x => x.DeleteAsync(image.ObjectKey))
            .Returns(Task.CompletedTask);

        _repo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _publisher.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _handler.Handle(
            new DeleteProductImageCommand(product.Id, image.Id),
            CancellationToken.None);

        product.Images.Should().BeEmpty();
    }
}