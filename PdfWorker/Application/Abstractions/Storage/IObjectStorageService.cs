namespace Application.Abstractions.Storage;

public interface IObjectStorageService
{
    Task<(string Bucket, string ObjectKey, long FileSizeBytes)> UploadAsync(
        byte[] content,
        string documentType,
        string contentType,
        CancellationToken cancellationToken = default);
}
