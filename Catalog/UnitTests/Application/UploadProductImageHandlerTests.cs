using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Application.Common;
using Application.Features.Products.UploadImage;
using Contracts.Events;
using Domain.Exceptions;
using FluentAssertions;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UnitTests.Helpers;

namespace UnitTests.Application;

public class UploadProductImageHandlerTests
{
    private readonly Mock<IProductRepository> _repo = new();
    private readonly Mock<IFileStorageService> _storage = new();
    private readonly Mock<IEventPublisher> _publisher = new();
    private readonly Mock<ICacheService> _cache = new();

    private readonly UploadProductImageHandler _handler;

    public UploadProductImageHandlerTests()
    {
        _handler = new UploadProductImageHandler(
            _repo.Object,
            _storage.Object,
            _publisher.Object,
            new ProductCacheInvalidator(_cache.Object, new AfterCommitQueue(NullLogger<AfterCommitQueue>.Instance)));

        _storage.Setup(x => x.UploadStreamAsync(
                It.IsAny<StorageBucket>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<long>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("url");
    }

    private static UploadProductImageCommand Command(Guid productId, byte[] bytes, bool isPrimary = false, string name = "file.png") =>
        new(productId, new MemoryStream(bytes), bytes.Length, name, "image/png", isPrimary);

    [Fact]
    public async Task First_Image_Becomes_Primary_With_Detected_Type()
    {
        var product = TestDataBuilder.CreateProduct();
        _repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        // The file is called .png but the bytes are JPEG: the bytes win.
        var result = await _handler.Handle(Command(product.Id, TestImages.Jpeg()), CancellationToken.None);

        result.Url.Should().Be("url");
        result.IsPrimary.Should().BeTrue();
        result.ContentType.Should().Be("image/jpeg");
        result.SortOrder.Should().Be(0);
        product.Images.Should().ContainSingle();
    }

    [Fact]
    public async Task Object_Key_Uses_Product_And_Image_Ids()
    {
        var product = TestDataBuilder.CreateProduct();
        _repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var result = await _handler.Handle(Command(product.Id, TestImages.Png()), CancellationToken.None);

        _storage.Verify(x => x.UploadStreamAsync(
            StorageBucket.Images,
            $"products/{product.Id}/images/{result.ImageId}",
            It.IsAny<Stream>(), It.IsAny<long>(), "image/png", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Later_Images_Are_Appended_And_Not_Primary()
    {
        var product = TestDataBuilder.CreateProduct();
        _repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        await _handler.Handle(Command(product.Id, TestImages.Png()), CancellationToken.None);
        var second = await _handler.Handle(Command(product.Id, TestImages.WebP()), CancellationToken.None);

        second.IsPrimary.Should().BeFalse();
        second.SortOrder.Should().Be(1);
        product.Images.Count(x => x.IsPrimary).Should().Be(1);
    }

    [Fact]
    public async Task Uploading_As_Primary_Replaces_The_Current_Primary()
    {
        var product = TestDataBuilder.CreateProduct();
        _repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var first = await _handler.Handle(Command(product.Id, TestImages.Png()), CancellationToken.None);
        var second = await _handler.Handle(Command(product.Id, TestImages.Png(), isPrimary: true), CancellationToken.None);

        second.IsPrimary.Should().BeTrue();
        product.Images.Single(x => x.Id == first.ImageId).IsPrimary.Should().BeFalse();
        product.Images.Count(x => x.IsPrimary).Should().Be(1);
    }

    [Fact]
    public async Task Rejects_An_Executable_Renamed_To_Jpg_With_Nothing_Stored()
    {
        var product = TestDataBuilder.CreateProduct();
        _repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var act = () => _handler.Handle(Command(product.Id, TestImages.Executable(), name: "evil.jpg"), CancellationToken.None);

        await act.Should().ThrowAsync<UnsupportedFileTypeException>().WithMessage("*JPEG, PNG or WebP*");
        _storage.Verify(x => x.UploadStreamAsync(
            It.IsAny<StorageBucket>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<long>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        product.Images.Should().BeEmpty();
    }

    [Fact]
    public async Task Rejects_A_File_Over_Five_Megabytes_Without_Reading_It()
    {
        var product = TestDataBuilder.CreateProduct();
        _repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var command = new UploadProductImageCommand(
            product.Id, new MemoryStream(TestImages.Png()), 5L * 1024 * 1024 + 1, "big.png", "image/png", false);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<PayloadTooLargeException>().WithMessage("*5 MB*");
        _storage.Verify(x => x.UploadStreamAsync(
            It.IsAny<StorageBucket>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<long>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Exactly_Five_Megabytes_Is_Accepted()
    {
        var product = TestDataBuilder.CreateProduct();
        _repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var command = new UploadProductImageCommand(
            product.Id, new MemoryStream(TestImages.Png()), 5L * 1024 * 1024, "limit.png", "image/png", false);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.ImageId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Removes_The_Stored_Object_When_The_Row_Cannot_Be_Written()
    {
        var product = TestDataBuilder.CreateProduct();
        _repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _repo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db down"));

        var act = () => _handler.Handle(Command(product.Id, TestImages.Png()), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _storage.Verify(x => x.DeleteAsync(StorageBucket.Images, It.Is<string>(k => k.StartsWith($"products/{product.Id}/images/")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Throws_When_Product_Not_Found()
    {
        _repo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Domain.Entities.Product?)null);

        var act = () => _handler.Handle(Command(Guid.NewGuid(), TestImages.Png()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Publishes_An_Event_And_Clears_The_Cache()
    {
        var product = TestDataBuilder.CreateProduct();
        _repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var result = await _handler.Handle(Command(product.Id, TestImages.Png()), CancellationToken.None);

        _publisher.Verify(x => x.PublishAsync(
            It.Is<ProductImageUploaded>(e => e.ProductId == product.Id && e.ImageId == result.ImageId && e.IsPrimary),
            "product.image.uploaded"), Times.Once);
        _cache.Verify(x => x.RemoveAsync($"product:{product.Id}"), Times.Once);
        _cache.Verify(x => x.RemoveByPrefixAsync("products:"), Times.Once);
    }
}
