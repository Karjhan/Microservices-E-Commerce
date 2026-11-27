namespace Infrastructure.Storage;

public static class FileSignatureValidator
{
    public const string Jpeg = "image/jpeg";
    public const string Png = "image/png";
    public const string WebP = "image/webp";

    public const int HeaderLength = 12;

    public static readonly IReadOnlyList<string> AllowedImageTypes = [Jpeg, Png, WebP];

    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] RiffSignature = "RIFF"u8.ToArray();
    private static readonly byte[] WebPSignature = "WEBP"u8.ToArray();

    public static string? DetectImageContentType(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(JpegSignature))
            return Jpeg;

        if (header.StartsWith(PngSignature))
            return Png;

        if (header.Length >= HeaderLength
            && header[..4].SequenceEqual(RiffSignature)
            && header.Slice(8, 4).SequenceEqual(WebPSignature))
        {
            return WebP;
        }

        return null;
    }

    public static async Task<string?> DetectImageContentTypeAsync(Stream content, CancellationToken ct = default)
    {
        var start = content.Position;
        var buffer = new byte[HeaderLength];
        var read = 0;

        while (read < buffer.Length)
        {
            var count = await content.ReadAsync(buffer.AsMemory(read), ct);
            if (count == 0)
                break;
            read += count;
        }

        content.Position = start;

        return DetectImageContentType(buffer.AsSpan(0, read));
    }

    public static string AllowedImageTypesDescription => "JPEG, PNG or WebP images up to 5 MB";
}
