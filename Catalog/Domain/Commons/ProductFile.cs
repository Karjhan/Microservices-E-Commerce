namespace Domain.Entities;

public class ProductFile
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid ProductId { get; private set; }

    public string FileName { get; private set; } = default!;
    public string ObjectKey { get; private set; } = default!;
    public string Url { get; private set; } = default!;

    public long FileSize { get; private set; }

    public string FileType { get; private set; } = default!;

    public Product Product { get; private set; } = null!;
}