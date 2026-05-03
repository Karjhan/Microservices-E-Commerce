using Domain.Commons;
using Domain.Enums;

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

    public PrintSettings Settings { get; private set; } = new();
    public Dimensions Size { get; private set; } = new();

    public List<string> Tags { get; private set; } = new();
    public List<ProductRelation> RelatedProducts { get; private set; } = new();
    public List<ProductAttribute> Attributes { get; private set; } = new();

    public List<MaterialType> SupportedMaterials { get; private set; } = new();
    public List<PrinterType> CompatiblePrinters { get; private set; } = new();

    public List<ProductImage> Images { get; private set; } = new();

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

    public void AddCompatibility(MaterialType? material, PrinterType? printerType)
    {
        if (material.HasValue && !SupportedMaterials.Contains(material.Value))
            SupportedMaterials.Add(material.Value);

        if (printerType.HasValue && !CompatiblePrinters.Contains(printerType.Value))
            CompatiblePrinters.Add(printerType.Value);
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

    public void SetSupportedMaterials(IEnumerable<MaterialType> materials)
    {
        SupportedMaterials = materials.Distinct().ToList();
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

    public bool RemoveCompatibility(MaterialType? material, PrinterType? printerType)
    {
        var removed = false;

        if (material.HasValue)
            removed |= SupportedMaterials.Remove(material.Value);

        if (printerType.HasValue)
            removed |= CompatiblePrinters.Remove(printerType.Value);

        return removed;
    }

    public void Publish()
    {
        if (Status != ProductStatus.Draft && Status != ProductStatus.Inactive)
            throw new InvalidOperationException("Only draft or inactive products can be published.");

        Status = ProductStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        if (Status != ProductStatus.Active)
            throw new InvalidOperationException("Only active products can be deactivated.");

        Status = ProductStatus.Inactive;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkOutOfStock()
    {
        Status = ProductStatus.OutOfStock;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Discontinue()
    {
        Status = ProductStatus.Discontinued;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddImage(ProductImage image)
    {
        if (image.IsPrimary)
        {
            foreach (var img in Images)
                img.SetPrimary(false);
        }

        Images.Add(image);
    }

    public bool RemoveImage(Guid imageId)
    {
        return Images.RemoveAll(x => x.Id == imageId) > 0;
    }

    private static string GenerateSlug(string name)
    {
        return name.ToLower()
            .Replace(" ", "-")
            .Replace(".", "")
            .Replace(",", "");
    }
}