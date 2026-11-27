namespace Application.Abstractions.Storage;

public enum StorageBucket
{
    Images,

    Models
}

public interface IFileStorageService
{
    Task<string> UploadAsync(string filePath, string objectKey, string contentType);
    Task DeleteAsync(string objectKey);

    Task EnsureBucketsAsync(CancellationToken ct = default);

    Task<string> UploadStreamAsync(
        StorageBucket bucket,
        string objectKey,
        Stream content,
        long size,
        string contentType,
        CancellationToken ct = default);

    Task DeleteAsync(StorageBucket bucket, string objectKey, CancellationToken ct = default);

    Task<string> GetPresignedUrlAsync(
        StorageBucket bucket,
        string objectKey,
        TimeSpan lifetime,
        CancellationToken ct = default);

    Task<IReadOnlyList<StoredObject>> ListObjectsAsync(StorageBucket bucket, CancellationToken ct = default);
}

public record StoredObject(string Key, DateTime LastModifiedUtc);
