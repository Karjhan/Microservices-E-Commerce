using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Application.Common;
using Application.Features.Products.DeleteImage;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UnitTests.Helpers;

namespace UnitTests.Application;

public class DeleteProductImageHandlerTests
{
    private readonly Mock<IProductRepository> _repo = new();
    private readonly Mock<IFileStorageService> _storage = new();
    private readonly Mock<IEventPublisher> _publisher = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly AfterCommitQueue _afterCommit = new(NullLogger<AfterCommitQueue>.Instance);

    private readonly DeleteProductImageHandler _handler;

    public DeleteProductImageHandlerTests()
    {
        _handler = new DeleteProductImageHandler(
            _repo.Object,
            _storage.Object,
            _publisher.Object,
            _afterCommit,
            new ProductCacheInvalidator(_cache.Object, _afterCommit));
    }

    private static ProductImage Image(Product product, string key) =>
        new(Guid.NewGuid(), product.Id, key, "url", "image/png", 10, 0, false);

    [Fact]
    public async Task Removes_The_Image_And_Deletes_The_File_Only_After_Commit()
    {
        var product = TestDataBuilder.CreateProduct();
        var image = Image(product, "object-key");
        product.AddImage(image);

        _repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        await _handler.Handle(new DeleteProductImageCommand(product.Id, image.Id), CancellationToken.None);

        product.Images.Should().BeEmpty();

        // Nothing is removed from storage until the surrounding transaction has committed.
        _storage.Verify(x => x.DeleteAsync(It.IsAny<StorageBucket>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        await _afterCommit.RunAsync(CancellationToken.None);

        _storage.Verify(x => x.DeleteAsync(StorageBucket.Images, "object-key", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Deleting_The_Primary_Promotes_The_Lowest_Sort_Order()
    {
        var product = TestDataBuilder.CreateProduct();
        var primary = Image(product, "a");
        var second = Image(product, "b");
        var third = Image(product, "c");
        product.AddImage(primary);
        product.AddImage(second);
        product.AddImage(third);

        _repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        await _handler.Handle(new DeleteProductImageCommand(product.Id, primary.Id), CancellationToken.None);

        product.Images.Should().HaveCount(2);
        product.Images.Single(x => x.IsPrimary).Id.Should().Be(second.Id);
    }

    [Fact]
    public async Task Deleting_A_Non_Primary_Keeps_The_Primary()
    {
        var product = TestDataBuilder.CreateProduct();
        var primary = Image(product, "a");
        var other = Image(product, "b");
        product.AddImage(primary);
        product.AddImage(other);

        _repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        await _handler.Handle(new DeleteProductImageCommand(product.Id, other.Id), CancellationToken.None);

        product.Images.Single().IsPrimary.Should().BeTrue();
    }

    [Fact]
    public async Task Throws_When_The_Image_Belongs_To_Another_Product()
    {
        var product = TestDataBuilder.CreateProduct();
        _repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var act = () => _handler.Handle(new DeleteProductImageCommand(product.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
