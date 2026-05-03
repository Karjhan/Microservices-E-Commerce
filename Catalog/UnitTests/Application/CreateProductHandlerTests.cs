using Application.Abstractions.Messaging;
using Application.Features.Products.CreateProduct;
using Domain.Commons;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using Moq;

namespace UnitTests.Application;

public class CreateProductHandlerTests
{
    private readonly Mock<IProductRepository> _repo = new();
    private readonly Mock<IEventPublisher> _publisher = new();
    private readonly Mock<ICacheService> _cache = new();

    private readonly CreateProductHandler _handler;

    public CreateProductHandlerTests()
    {
        _handler = new CreateProductHandler(
            _repo.Object,
            _publisher.Object,
            _cache.Object);
    }

    [Fact]
    public async Task Should_Create_Product_And_Return_Id()
    {
        // Arrange
        var command = new CreateProductCommand(
            "Name",
            "Short",
            "Long",
            100,
            Guid.NewGuid(),
            "EUR",
            new PrintSettings(),
            new Dimensions(),
            new[] { "tag1" },
            new[] { MaterialType.PLA },
            new[] { PrinterType.FDM }
        );

        _repo.Setup(x => x.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _repo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _cache.Setup(x => x.RemoveByPrefixAsync("products:"))
            .Returns(Task.CompletedTask);

        _publisher.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();

        _repo.Verify(x => x.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(x => x.RemoveByPrefixAsync("products:"), Times.Once);
    }
}