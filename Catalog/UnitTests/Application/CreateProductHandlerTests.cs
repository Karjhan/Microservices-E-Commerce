using Application.Abstractions.Messaging;
using Application.Features.Products.CreateProduct;
using Domain.Commons;
using Domain.Entities;
using Domain.Exceptions;
using FluentAssertions;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using Moq;

namespace UnitTests.Application;

public class CreateProductHandlerTests
{
    private static readonly Guid CategoryId = Guid.NewGuid();

    private readonly Mock<IProductRepository> _repo = new();
    private readonly Mock<ICategoryRepository> _categories = new();
    private readonly Mock<IBrandRepository> _brands = new();
    private readonly Mock<IFilamentTypeRepository> _filamentTypes = new();
    private readonly Mock<IEventPublisher> _publisher = new();
    private readonly Mock<ICacheService> _cache = new();

    private readonly CreateProductHandler _handler;

    public CreateProductHandlerTests()
    {
        _handler = new CreateProductHandler(
            _repo.Object,
            _categories.Object,
            _brands.Object,
            _filamentTypes.Object,
            _publisher.Object,
            _cache.Object);
    }

    private static CreateProductCommand Command(Guid? brandId = null, Guid[]? filamentTypeIds = null) => new(
        "Name",
        "Short",
        "Long",
        100,
        CategoryId,
        "EUR",
        new PrintSettings(),
        new Dimensions(),
        new[] { "tag1" },
        new[] { PrinterType.FDM },
        brandId,
        filamentTypeIds);

    private void GivenCategory(bool active = true)
    {
        var category = new Category(CategoryId, "Figurines");
        category.SetActive(active);

        _categories.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([category]);
    }

    [Fact]
    public async Task Should_Create_Product_And_Return_Id()
    {
        // Arrange
        GivenCategory();

        var command = Command();

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

    [Fact]
    public async Task Should_Refuse_A_Category_That_Does_Not_Exist()
    {
        _categories.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var act = () => _handler.Handle(Command(), CancellationToken.None);

        var error = (await act.Should().ThrowAsync<ValidationFailedException>()).Which;
        error.Errors.Should().ContainKey("categoryId");
        _repo.Verify(x => x.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Should_Refuse_A_Deactivated_Category_For_A_New_Product()
    {
        GivenCategory(active: false);

        var act = () => _handler.Handle(Command(), CancellationToken.None);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be("reference_inactive");
    }

    [Fact]
    public async Task Should_Refuse_A_Deactivated_Brand_For_A_New_Product()
    {
        GivenCategory();

        var brand = new Brand("Prusa");
        brand.SetActive(false);

        _brands.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([brand]);

        var act = () => _handler.Handle(Command(brandId: brand.Id), CancellationToken.None);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be("reference_inactive");
    }

    [Fact]
    public async Task Should_Refuse_An_Unknown_Filament_Type()
    {
        GivenCategory();

        _filamentTypes.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var act = () => _handler.Handle(Command(filamentTypeIds: [Guid.NewGuid()]), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationFailedException>()).Which.Errors.Should().ContainKey("filamentTypeIds");
    }
}
