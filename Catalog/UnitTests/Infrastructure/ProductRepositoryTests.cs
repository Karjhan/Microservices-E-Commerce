using Domain.Commons;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace UnitTests.Infrastructure;

public class ProductRepositoryTests
{
    private static CatalogDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlite("Filename=:memory:")
            .Options;

        var context = new CatalogDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();

        return context;
    }

    [Fact]
    public async Task AddAsync_ShouldPersistProduct()
    {
        // Arrange
        var context = CreateDbContext();
        var repo = new ProductRepository(context);

        var product = new Product("Test", "Short", "Long", 10, Guid.NewGuid());

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

        var product = new Product("Test", "Short", "Long", 10, Guid.NewGuid());

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

        var p1 = new Product("Cheap", "s", "l", 5, Guid.NewGuid());
        var p2 = new Product("Expensive", "s", "l", 100, Guid.NewGuid());

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

        var product = new Product("Tagged", "s", "l", 10, Guid.NewGuid());
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

        var product = new Product("Test", "s", "l", 10, Guid.NewGuid());

        await repo.AddAsync(product, default);
        await repo.SaveChangesAsync(default);

        // Act
        await repo.DeleteAsync(product, default);
        await repo.SaveChangesAsync(default);

        // Assert
        context.Products.Should().BeEmpty();
    }
}