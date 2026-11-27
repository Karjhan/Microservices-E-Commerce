namespace Domain.Entities;

public class Brand : ReferenceEntity
{
    public string? LogoObjectKey { get; private set; }
    public string? LogoUrl { get; private set; }

    private Brand() { }

    public Brand(string name) : base(name) { }

    public static string BuildLogoKey(Guid brandId, Guid logoId) => $"brands/{brandId}/logo/{logoId}";

    public void SetLogo(string objectKey, string url)
    {
        LogoObjectKey = objectKey;
        LogoUrl = url;
        Touch();
    }

    public void ClearLogo()
    {
        LogoObjectKey = null;
        LogoUrl = null;
        Touch();
    }
}
