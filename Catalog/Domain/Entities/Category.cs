namespace Domain.Entities;

public class Category : ReferenceEntity
{
    private Category() { }

    public Category(string name) : base(name) { }

    /// <summary>Creates a category that reuses an id already referenced by products (data migration, tests).</summary>
    public Category(Guid id, string name) : base(name)
    {
        Id = id;
    }
}
