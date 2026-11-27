using Application.Common;
using Application.Features.Brands.CreateBrand;
using Application.Features.Brands.DeleteBrand;
using Application.Features.Categories.DeleteCategory;
using Application.Features.Categories.SetCategoryActive;
using Application.Features.Categories.UpdateCategory;
using Application.Features.FilamentTypes.CreateFilamentType;
using Application.Abstractions.Storage;
using Domain.Entities;
using Domain.Exceptions;
using FluentAssertions;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Abstractions.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace UnitTests.Application;

public class ReferenceDataHandlerTests
{
    private readonly Mock<IBrandRepository> _brands = new();
    private readonly Mock<ICategoryRepository> _categories = new();
    private readonly Mock<IFilamentTypeRepository> _filamentTypes = new();

    [Fact]
    public async Task CreateBrand_Should_Refuse_A_Name_That_Differs_Only_In_Case()
    {
        _brands.Setup(x => x.NameExistsAsync("prusa", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var handler = new CreateBrandHandler(_brands.Object);

        var act = () => handler.Handle(new CreateBrandCommand("  PRUSA "), CancellationToken.None);

        var error = (await act.Should().ThrowAsync<ConflictException>()).Which;
        error.Code.Should().Be("duplicate_name");
        error.Errors.Should().ContainKey("name");
        _brands.Verify(x => x.AddAsync(It.IsAny<Brand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateBrand_Should_Store_The_Cleaned_Name()
    {
        Brand? added = null;
        _brands.Setup(x => x.AddAsync(It.IsAny<Brand>(), It.IsAny<CancellationToken>()))
            .Callback<Brand, CancellationToken>((b, _) => added = b)
            .Returns(Task.CompletedTask);

        var handler = new CreateBrandHandler(_brands.Object);

        var id = await handler.Handle(new CreateBrandCommand("  Bambu   Lab "), CancellationToken.None);

        added!.Id.Should().Be(id);
        added.Name.Should().Be("Bambu Lab");
        added.NormalizedName.Should().Be("bambu lab");
        added.IsActive.Should().BeTrue();
        _brands.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" a ")]
    public async Task CreateBrand_Should_Refuse_A_Name_That_Is_Too_Short(string name)
    {
        var handler = new CreateBrandHandler(_brands.Object);

        var act = () => handler.Handle(new CreateBrandCommand(name), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationFailedException>()).Which.Errors.Should().ContainKey("name");
    }

    [Fact]
    public async Task DeleteCategory_Should_Refuse_An_Entry_That_Products_Use_And_Report_The_Count()
    {
        var category = new Category("Figurines");
        _categories.Setup(x => x.GetByIdAsync(category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(category);
        _categories.Setup(x => x.CountProductsUsingAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [category.Id] = 12 });

        var handler = new DeleteCategoryHandler(_categories.Object);

        var act = () => handler.Handle(new DeleteCategoryCommand(category.Id), CancellationToken.None);

        var error = (await act.Should().ThrowAsync<ConflictException>()).Which;
        error.Code.Should().Be("in_use");
        error.Details["productCount"].Should().Be(12);
        error.Message.Should().Contain("12");
        _categories.Verify(x => x.Remove(It.IsAny<Category>()), Times.Never);
    }

    [Fact]
    public async Task DeleteCategory_Should_Remove_An_Unused_Entry()
    {
        var category = new Category("Figurines");
        _categories.Setup(x => x.GetByIdAsync(category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(category);
        _categories.Setup(x => x.CountProductsUsingAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [category.Id] = 0 });

        var handler = new DeleteCategoryHandler(_categories.Object);

        await handler.Handle(new DeleteCategoryCommand(category.Id), CancellationToken.None);

        _categories.Verify(x => x.Remove(category), Times.Once);
        _categories.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteBrand_Should_Refuse_An_Entry_That_Products_Use()
    {
        var brand = new Brand("Prusa");
        _brands.Setup(x => x.GetByIdAsync(brand.Id, It.IsAny<CancellationToken>())).ReturnsAsync(brand);
        _brands.Setup(x => x.CountProductsUsingAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [brand.Id] = 1 });

        var handler = new DeleteBrandHandler(
            _brands.Object, new Mock<IFileStorageService>().Object, new AfterCommitQueue(NullLogger<AfterCommitQueue>.Instance));

        var act = () => handler.Handle(new DeleteBrandCommand(brand.Id), CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Code.Should().Be("in_use");
    }

    [Fact]
    public async Task DeleteBrand_Should_Remove_The_Logo_File_After_Commit()
    {
        var brand = new Brand("Prusa");
        brand.SetLogo("brands/x/logo/y", "http://files/logo");

        _brands.Setup(x => x.GetByIdAsync(brand.Id, It.IsAny<CancellationToken>())).ReturnsAsync(brand);
        _brands.Setup(x => x.CountProductsUsingAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [brand.Id] = 0 });

        var storage = new Mock<IFileStorageService>();
        var afterCommit = new AfterCommitQueue(NullLogger<AfterCommitQueue>.Instance);

        var handler = new DeleteBrandHandler(_brands.Object, storage.Object, afterCommit);

        await handler.Handle(new DeleteBrandCommand(brand.Id), CancellationToken.None);

        storage.Verify(x => x.DeleteAsync(It.IsAny<StorageBucket>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        await afterCommit.RunAsync(CancellationToken.None);

        storage.Verify(x => x.DeleteAsync(StorageBucket.Images, "brands/x/logo/y", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateCategory_Should_Refuse_Another_Entrys_Name_But_Allow_Its_Own()
    {
        var category = new Category("Figurines");
        _categories.Setup(x => x.GetByIdAsync(category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(category);
        _categories.Setup(x => x.NameExistsAsync("tools", category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _categories.Setup(x => x.NameExistsAsync("figurines", category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var cache = new ProductCacheInvalidator(new Mock<ICacheService>().Object, new AfterCommitQueue(NullLogger<AfterCommitQueue>.Instance));
        var handler = new UpdateCategoryHandler(_categories.Object, cache);

        var clash = () => handler.Handle(new UpdateCategoryCommand(category.Id, "TOOLS"), CancellationToken.None);
        (await clash.Should().ThrowAsync<ConflictException>()).Which.Code.Should().Be("duplicate_name");

        await handler.Handle(new UpdateCategoryCommand(category.Id, "FIGURINES"), CancellationToken.None);
        category.Name.Should().Be("FIGURINES");
    }

    [Fact]
    public async Task SetCategoryActive_Should_Deactivate_Without_Touching_Products()
    {
        var category = new Category("Figurines");
        _categories.Setup(x => x.GetByIdAsync(category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(category);

        var handler = new SetCategoryActiveHandler(_categories.Object);

        await handler.Handle(new SetCategoryActiveCommand(category.Id, false), CancellationToken.None);

        category.IsActive.Should().BeFalse();
        _categories.Verify(x => x.CountProductsUsingAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateFilamentType_Should_Derive_The_Code_From_The_Name()
    {
        FilamentType? added = null;
        _filamentTypes.Setup(x => x.AddAsync(It.IsAny<FilamentType>(), It.IsAny<CancellationToken>()))
            .Callback<FilamentType, CancellationToken>((f, _) => added = f)
            .Returns(Task.CompletedTask);

        var handler = new CreateFilamentTypeHandler(_filamentTypes.Object);

        await handler.Handle(
            new CreateFilamentTypeCommand("Silk PLA", null, 1.75m, ["Gold", "gold", " Silver "]), CancellationToken.None);

        added!.Code.Should().Be("SilkPLA");
        added.DiameterMm.Should().Be(1.75m);
        added.ColorOptions.Should().Equal("Gold", "Silver");
    }

    [Fact]
    public async Task CreateFilamentType_Should_Refuse_A_Code_That_Is_Taken()
    {
        _filamentTypes.Setup(x => x.CodeExistsAsync("PLA", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var handler = new CreateFilamentTypeHandler(_filamentTypes.Object);

        var act = () => handler.Handle(new CreateFilamentTypeCommand("Plain PLA", "PLA", null, null), CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Code.Should().Be("duplicate_code");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(11)]
    [InlineData(1.755)]
    public async Task CreateFilamentType_Should_Refuse_An_Invalid_Diameter(double diameter)
    {
        var handler = new CreateFilamentTypeHandler(_filamentTypes.Object);

        var act = () => handler.Handle(
            new CreateFilamentTypeCommand("Odd", "ODD", (decimal)diameter, null), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationFailedException>()).Which.Errors.Should().ContainKey("diameterMm");
    }

    [Fact]
    public async Task ProductRules_Should_Let_A_Product_Keep_A_Deactivated_Brand_But_Not_Take_One()
    {
        var brand = new Brand("Prusa");
        brand.SetActive(false);

        _brands.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([brand]);

        // The product already has this brand: still valid.
        await ProductReferenceRules.EnsureBrandAsync(_brands.Object, brand.Id, brand.Id, CancellationToken.None);

        // A product that does not have it cannot be given it.
        var act = () => ProductReferenceRules.EnsureBrandAsync(_brands.Object, brand.Id, null, CancellationToken.None);
        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be("reference_inactive");
    }

    [Fact]
    public async Task ProductRules_Should_Let_A_Product_Keep_Deactivated_Filament_Types()
    {
        var kept = new FilamentType("PLA", "PLA", null, null);
        kept.SetActive(false);

        _filamentTypes.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([kept]);

        var ids = await ProductReferenceRules.EnsureFilamentTypesAsync(
            _filamentTypes.Object, [kept.Id, kept.Id], [kept.Id], CancellationToken.None);

        ids.Should().Equal(kept.Id);

        var act = () => ProductReferenceRules.EnsureFilamentTypesAsync(
            _filamentTypes.Object, [kept.Id], [], CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>();
    }
}
