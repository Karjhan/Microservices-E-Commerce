using Domain.Commons;
using Domain.Enums;
using Domain.Exceptions;

namespace Domain.Entities;

public class Product
{
    public Guid Id { get; private set; }

    public string Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;

    public string ShortDescription { get; private set; } = default!;
    public string LongDescription { get; private set; } = default!;

    public decimal Price { get; private set; }
    public string Currency { get; private set; } = "EUR";

    public ProductStatus Status { get; private set; }

    public Guid CategoryId { get; private set; }
    public Category? Category { get; private set; }

    public Guid? BrandId { get; private set; }
    public Brand? Brand { get; private set; }

    public PrintSettings Settings { get; private set; } = new();
    public Dimensions Size { get; private set; } = new();

    public List<string> Tags { get; private set; } = new();
    public List<ProductRelation> RelatedProducts { get; private set; } = new();
    public List<ProductAttribute> Attributes { get; private set; } = new();

    public List<PrinterType> CompatiblePrinters { get; private set; } = new();

    public List<ProductFilamentType> FilamentTypes { get; private set; } = new();

    public List<ProductImage> Images { get; private set; } = new();
    public ProductModel? Model { get; private set; }

    public int DownloadCount { get; private set; } = 0;
    public double AverageRating { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Product() { }

    public Product(
        string name,
        string shortDescription,
        string longDescription,
        decimal price,
        Guid categoryId)
    {
        Id = Guid.NewGuid();

        Name = name;
        Slug = GenerateSlug(name);

        ShortDescription = shortDescription;
        LongDescription = longDescription;

        Price = price;
        CategoryId = categoryId;

        Status = ProductStatus.Draft;
        CreatedAt = DateTime.UtcNow;

        AverageRating = 0;
        DownloadCount = 0;
    }

    public void UpdateDetails(string name, string shortDescription, string longDescription, decimal price)
    {
        Name = name;
        Slug = GenerateSlug(name);

        ShortDescription = shortDescription;
        LongDescription = longDescription;

        Price = price;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddAttribute(ProductAttribute attribute)
    {
        if (Attributes.All(a => a.Key != attribute.Key))
            Attributes.Add(attribute);
    }

    public bool RemoveAttribute(Guid attributeId)
    {
        return Attributes.RemoveAll(a => a.Id == attributeId) > 0;
    }

    public void AddRelatedProduct(ProductRelation relation)
    {
        if (RelatedProducts.All(p => p.RelatedProductId != relation.RelatedProductId))
            RelatedProducts.Add(relation);
    }

    public bool RemoveRelatedProduct(Guid relationId)
    {
        return RelatedProducts.RemoveAll(r => r.Id == relationId) > 0;
    }

    public void AddCompatibility(PrinterType printerType)
    {
        if (!CompatiblePrinters.Contains(printerType))
            CompatiblePrinters.Add(printerType);
    }
    
    public void SetTags(IEnumerable<string> tags)
    {
        Tags = tags
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct()
            .ToList();
    }

    public void SetDownloadCount(int downloadCount)
    {
        DownloadCount = downloadCount;
    }

    public void SetAverageRating(double averageRating)
    {
        AverageRating = averageRating;
    }

    public void SetCategory(Guid categoryId)
    {
        CategoryId = categoryId;
    }

    public void SetBrand(Guid? brandId)
    {
        BrandId = brandId;
    }

    public void SetFilamentTypes(IEnumerable<Guid> filamentTypeIds)
    {
        var wanted = filamentTypeIds.ToHashSet();

        FilamentTypes.RemoveAll(x => !wanted.Contains(x.FilamentTypeId));

        foreach (var id in wanted.Where(id => FilamentTypes.All(x => x.FilamentTypeId != id)))
            FilamentTypes.Add(new ProductFilamentType(Id, id));
    }

    public void SetCompatiblePrinters(IEnumerable<PrinterType> printers)
    {
        CompatiblePrinters = printers.Distinct().ToList();
    }

    public void SetSettings(PrintSettings settings)
    {
        Settings = settings;
    }

    public void SetSize(Dimensions size)
    {
        Size = size;
    }

    public void SetCurrency(string currency)
    {
        Currency = currency;
    }

    public bool RemoveCompatibility(PrinterType printerType) => CompatiblePrinters.Remove(printerType);

    public bool ChangeStatus(ProductStatus target)
    {
        if (target == Status)
            return false;

        if (!ProductStatusRules.CanTransition(Status, target))
            throw new BusinessRuleException(
                "invalid_status_transition",
                $"A {Status} product cannot be changed to {target}.");

        Status = target;
        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    public void AddImage(ProductImage image)
    {
        image.SetSortOrder(Images.Count == 0 ? 0 : Images.Max(x => x.SortOrder) + 1);

        if (Images.Count == 0)
            image.SetPrimary(true);

        if (image.IsPrimary)
            ClearPrimaryImage();

        Images.Add(image);
    }

    public void ClearPrimaryImage()
    {
        foreach (var img in Images)
            img.SetPrimary(false);
    }

    public void SetPrimaryImage(Guid imageId)
    {
        var image = Images.FirstOrDefault(x => x.Id == imageId)
                    ?? throw new KeyNotFoundException("Image not found for this product");

        ClearPrimaryImage();
        image.SetPrimary(true);
        UpdatedAt = DateTime.UtcNow;
    }

    public void ReorderImages(IReadOnlyList<Guid> imageIds)
    {
        var expected = Images.Select(x => x.Id).ToHashSet();

        if (imageIds.Count != expected.Count || !expected.SetEquals(imageIds))
            throw new ValidationFailedException(
                "imageIds",
                "Provide every image id of the product exactly once.");

        for (var index = 0; index < imageIds.Count; index++)
            Images.First(x => x.Id == imageIds[index]).SetSortOrder(index);

        UpdatedAt = DateTime.UtcNow;
    }

    public ProductImage? RemoveImage(Guid imageId)
    {
        var image = Images.FirstOrDefault(x => x.Id == imageId);

        if (image is not null)
            Images.Remove(image);

        return image;
    }

    public ProductImage? PromoteLowestImage()
    {
        if (Images.Count == 0 || Images.Any(x => x.IsPrimary))
            return null;

        var next = Images.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).First();
        next.SetPrimary(true);
        return next;
    }

    public void SetModel(ProductModel? model)
    {
        Model = model;
        UpdatedAt = DateTime.UtcNow;
    }
    public static string GenerateSlug(string name)
    {
        return name.ToLower()
            .Replace(" ", "-")
            .Replace(".", "")
            .Replace(",", "");
    }
}