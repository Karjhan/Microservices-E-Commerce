using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Application.Features.Products.UploadImage;
using FluentAssertions;
using Infrastructure.Abstractions.Persistence;
using Moq;
using UnitTests.Helpers;

namespace UnitTests.Application;

public class UploadProductImageHandlerTests
{
    private readonly Mock<IProductRepository> _repo = new();
    private readonly Mock<IFileStorageService> _storage = new();
    private readonly Mock<IEventPublisher> _publisher = new();

    private readonly UploadProductImageHandler _handler;

    public UploadProductImageHandlerTests()
    {
        _handler = new UploadProductImageHandler(
            _repo.Object,
            _storage.Object,
            _publisher.Object);
    }

    [Fact]
    public async Task Should_Upload_Image()
    {
        var product = TestDataBuilder.CreateProduct();

        _repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _storage.Setup(x => x.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("url");

        _repo.Setup(x => x.AddImageAsync(It.IsAny<Domain.Entities.ProductImage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _repo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _publisher.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var cmd = new UploadProductImageCommand(
            product.Id,
            "/tmp/file.png",
            "file.png",
            "image/png",
            true);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Url.Should().Be("url");
    }
}