using Domain.Commons;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace UnitTests.Infrastructure;

public class ProductRepositoryTests
{
    // Products point at a category by foreign key, so every test database has this one.
    private static readonly Guid CategoryId = Guid.NewGuid();

    private static CatalogDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlite("Filename=:memory:")
            .Options;

        var context = new CatalogDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();

        context.Categories.Add(new Category(CategoryId, "Figurines"));
        context.SaveChanges();

        return context;
    }

    [Fact]
    public async Task AddAsync_ShouldPersistProduct()
    {
        // Arrange
        var context = CreateDbContext();
        var repo = new ProductRepository(context);

        var product = new Product("Test", "Short", "Long", 10, CategoryId);

        // Act
        await repo.AddAsync(product, default);
        await repo.SaveChangesAsync(default);

        // Assert
        context.Products.Count().Should().Be(1);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnProductWithIncludes()
    {
        // Arrange
        var context = CreateDbContext();
        var repo = new ProductRepository(context);

        var product = new Product("Test", "Short", "Long", 10, CategoryId);

        product.AddImage(new ProductImage(product.Id, "key", "url", true));
        product.AddAttribute(new ProductAttribute(product.Id, "color", "red"));

        await repo.AddAsync(product, default);
        await repo.SaveChangesAsync(default);

        // Act
        var result = await repo.GetByIdAsync(product.Id, default);

        // Assert
        result.Should().NotBeNull();
        result!.Images.Should().HaveCount(1);
        result.Attributes.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetFilteredAsync_ShouldFilterByPrice()
    {
        // Arrange
        var context = CreateDbContext();
        var repo = new ProductRepository(context);

        var p1 = new Product("Cheap", "s", "l", 5, CategoryId);
        var p2 = new Product("Expensive", "s", "l", 100, CategoryId);

        await repo.AddAsync(p1, default);
        await repo.AddAsync(p2, default);
        await repo.SaveChangesAsync(default);

        var filter = new ProductFilter
        {
            MinPrice = 10
        };

        // Act
        var result = await repo.GetFilteredAsync(filter, default);

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("Expensive");
    }

    [Fact]
    public async Task GetFilteredAsync_ShouldFilterByTags()
    {
        // Arrange
        var context = CreateDbContext();
        var repo = new ProductRepository(context);

        var product = new Product("Tagged", "s", "l", 10, CategoryId);
        product.SetTags(new[] { "vase", "decor" });

        await repo.AddAsync(product, default);
        await repo.SaveChangesAsync(default);

        var filter = new ProductFilter
        {
            Tags = new List<string> { "vase" }
        };

        // Act
        var result = await repo.GetFilteredAsync(filter, default);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveProduct()
    {
        // Arrange
        var context = CreateDbContext();
        var repo = new ProductRepository(context);

        var product = new Product("Test", "s", "l", 10, CategoryId);

        await repo.AddAsync(product, default);
        await repo.SaveChangesAsync(default);

        // Act
        await repo.DeleteAsync(product, default);
        await repo.SaveChangesAsync(default);

        // Assert
        context.Products.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFilteredAsync_ShouldFilterByBrand()
    {
        var context = CreateDbContext();
        var repo = new ProductRepository(context);

        var brand = new Brand("Prusa");
        context.Brands.Add(brand);

        var branded = new Product("Branded", "s", "l", 10, CategoryId);
        branded.SetBrand(brand.Id);

        await repo.AddAsync(branded, default);
        await repo.AddAsync(new Product("Plain", "s", "l", 10, CategoryId), default);
        await repo.SaveChangesAsync(default);

        var result = await repo.GetFilteredAsync(new ProductFilter { BrandId = brand.Id }, default);

        result.Should().ContainSingle().Which.Name.Should().Be("Branded");
        result[0].Brand!.Name.Should().Be("Prusa");
    }

    [Fact]
    public async Task GetFilteredAsync_ShouldFilterByFilamentTypeCode_IgnoringCase()
    {
        var context = CreateDbContext();
        var repo = new ProductRepository(context);

        var pla = new FilamentType("PLA", "PLA", 1.75m, null);
        var abs = new FilamentType("ABS", "ABS", 1.75m, null);
        context.FilamentTypes.AddRange(pla, abs);

        var plaProduct = new Product("In PLA", "s", "l", 10, CategoryId);
        plaProduct.SetFilamentTypes([pla.Id]);

        var absProduct = new Product("In ABS", "s", "l", 10, CategoryId);
        absProduct.SetFilamentTypes([abs.Id]);

        await repo.AddAsync(plaProduct, default);
        await repo.AddAsync(absProduct, default);
        await repo.SaveChangesAsync(default);

        var result = await repo.GetFilteredAsync(new ProductFilter { FilamentType = "pla" }, default);

        result.Should().ContainSingle().Which.Name.Should().Be("In PLA");
        result[0].FilamentTypes.Single().FilamentType.Code.Should().Be("PLA");
    }

    [Fact]
    public async Task GetAdminPageAsync_ShouldFilterByBrandAndFilamentType()
    {
        var context = CreateDbContext();
        var repo = new ProductRepository(context);

        var brand = new Brand("Prusa");
        var pla = new FilamentType("PLA", "PLA", null, null);
        context.Brands.Add(brand);
        context.FilamentTypes.Add(pla);

        var match = new Product("Match", "s", "l", 10, CategoryId);
        match.SetBrand(brand.Id);
        match.SetFilamentTypes([pla.Id]);

        var brandOnly = new Product("Brand only", "s", "l", 10, CategoryId);
        brandOnly.SetBrand(brand.Id);

        await repo.AddAsync(match, default);
        await repo.AddAsync(brandOnly, default);
        await repo.SaveChangesAsync(default);

        var (items, total) = await repo.GetAdminPageAsync(
            new AdminProductFilter { BrandId = brand.Id, FilamentType = "PLA" }, default);

        total.Should().Be(1);
        items.Should().ContainSingle().Which.Name.Should().Be("Match");
    }
}