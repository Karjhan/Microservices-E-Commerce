using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Application.Common;
using Application.Features.Products.DeleteProduct;
using Contracts.Events;
using Domain.Commons;
using Domain.Entities;
using Domain.Exceptions;
using FluentAssertions;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UnitTests.Helpers;

namespace UnitTests.Application;

public class DeleteProductHandlerTests
{
    private readonly Mock<IProductRepository> _repo = new();
    private readonly Mock<IFileStorageService> _storage = new();
    private readonly Mock<IEventPublisher> _publisher = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly AfterCommitQueue _afterCommit = new(NullLogger<AfterCommitQueue>.Instance);

    private readonly DeleteProductHandler _handler;

    public DeleteProductHandlerTests()
    {
        _handler = new DeleteProductHandler(
            _repo.Object,
            _storage.Object,
            _publisher.Object,
            _cache.Object,
            _afterCommit);
    }

    private void SetupCommonMocks(Product product, IReadOnlyList<ProductRelation>? incomingRelations = null)
    {
        _repo.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _repo.Setup(x => x.RemoveIncomingRelationsAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(incomingRelations ?? []);

        _repo.Setup(x => x.DeleteAsync(product, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _repo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _storage.Setup(x => x.DeleteAsync(It.IsAny<StorageBucket>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _cache.Setup(x => x.RemoveAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _cache.Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        _publisher.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Should_Delete_Product_And_All_Images()
    {
        var product = TestDataBuilder.CreateProduct();
        product.AddImage(new ProductImage(product.Id, "img1", "url1", false));

        SetupCommonMocks(product);

        await _handler.Handle(new DeleteProductCommand(product.Id, true), CancellationToken.None);

        _repo.Verify(x => x.DeleteAsync(product, It.IsAny<CancellationToken>()), Times.Once);

        // Files are removed only once the delete has committed.
        _storage.Verify(x => x.DeleteAsync(It.IsAny<StorageBucket>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        await _afterCommit.RunAsync(CancellationToken.None);
        _storage.Verify(x => x.DeleteAsync(StorageBucket.Images, "img1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Should_Remove_The_3D_Model_File_Too()
    {
        var product = TestDataBuilder.CreateProduct();
        var model = new ProductModel(Guid.NewGuid(), product.Id, "glb", "part.glb", 1000);
        product.SetModel(model);

        SetupCommonMocks(product);

        await _handler.Handle(new DeleteProductCommand(product.Id, true), CancellationToken.None);
        await _afterCommit.RunAsync(CancellationToken.None);

        _storage.Verify(x => x.DeleteAsync(StorageBucket.Models, model.ObjectKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Should_Refuse_Without_Confirmation_And_Change_Nothing()
    {
        var product = TestDataBuilder.CreateProduct();
        SetupCommonMocks(product);

        var act = () => _handler.Handle(new DeleteProductCommand(product.Id), CancellationToken.None);

        var error = (await act.Should().ThrowAsync<ValidationFailedException>()).Which;
        error.Errors.Should().ContainKey("confirm");
        _repo.Verify(x => x.DeleteAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _publisher.Verify(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Should_Throw_When_Product_Not_Found()
    {
        _repo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _handler.Handle(new DeleteProductCommand(Guid.NewGuid(), true), CancellationToken.None));
    }

    [Fact]
    public async Task Should_Remove_Incoming_Relations_Before_Save()
    {
        var product = TestDataBuilder.CreateProduct();
        SetupCommonMocks(product);

        await _handler.Handle(new DeleteProductCommand(product.Id, true), CancellationToken.None);

        _repo.Verify(x => x.RemoveIncomingRelationsAsync(product.Id, It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Should_Publish_Attribute_Deleted_Events()
    {
        var product = TestDataBuilder.CreateProduct();
        product.AddAttribute(new ProductAttribute(product.Id, "color", "red"));
        product.AddAttribute(new ProductAttribute(product.Id, "size", "M"));

        SetupCommonMocks(product);

        await _handler.Handle(new DeleteProductCommand(product.Id, true), CancellationToken.None);

        _publisher.Verify(
            x => x.PublishAsync(It.Is<ProductAttributeDeleted>(e => e.ProductId == product.Id), "product.attribute.deleted"),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Should_Publish_Relation_Deleted_Events_For_Outgoing_Relations()
    {
        var product = TestDataBuilder.CreateProduct();
        var otherId = Guid.NewGuid();
        product.AddRelatedProduct(new ProductRelation(product.Id, otherId, "ACCESSORY"));

        SetupCommonMocks(product);

        await _handler.Handle(new DeleteProductCommand(product.Id, true), CancellationToken.None);

        _publisher.Verify(
            x => x.PublishAsync(
                It.Is<ProductRelationDeleted>(e => e.ProductId == product.Id && e.RelatedProductId == otherId),
                "product.relation.deleted"),
            Times.Once);
    }

    [Fact]
    public async Task Should_Publish_Relation_Deleted_Events_For_Incoming_Relations()
    {
        var product = TestDataBuilder.CreateProduct();
        var ownerId = Guid.NewGuid();
        var incoming = new List<ProductRelation>
        {
            new ProductRelation(ownerId, product.Id, "ACCESSORY")
        };

        SetupCommonMocks(product, incoming);

        await _handler.Handle(new DeleteProductCommand(product.Id, true), CancellationToken.None);

        _publisher.Verify(
            x => x.PublishAsync(
                It.Is<ProductRelationDeleted>(e => e.ProductId == ownerId && e.RelatedProductId == product.Id),
                "product.relation.deleted"),
            Times.Once);
    }

    [Fact]
    public async Task Should_Publish_Product_Deleted_Event()
    {
        var product = TestDataBuilder.CreateProduct();
        SetupCommonMocks(product);

        await _handler.Handle(new DeleteProductCommand(product.Id, true), CancellationToken.None);

        _publisher.Verify(
            x => x.PublishAsync(It.Is<ProductDeleted>(e => e.ProductId == product.Id), "product.deleted"),
            Times.Once);
    }

    [Fact]
    public async Task Should_Invalidate_Cache_After_Delete()
    {
        var product = TestDataBuilder.CreateProduct();
        SetupCommonMocks(product);

        await _handler.Handle(new DeleteProductCommand(product.Id, true), CancellationToken.None);

        _cache.Verify(x => x.RemoveAsync($"product:{product.Id}"), Times.Once);
        _cache.Verify(x => x.RemoveByPrefixAsync("products:"), Times.Once);
    }
}