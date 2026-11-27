using Application.Abstractions.Storage;
using Contracts;
using Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace Infrastructure.Storage;

public class MinioFileStorageService : IFileStorageService
{
    private const string PresignRegion = "us-east-1";

    private readonly IMinioClient _minio;
    private readonly MinioOptions _options;
    private readonly Lazy<IMinioClient> _presignClient;

    public MinioFileStorageService(
        IMinioClient minio,
        IOptions<MinioOptions> options)
    {
        _minio = minio;
        _options = options.Value;
        _presignClient = new Lazy<IMinioClient>(BuildPresignClient);
    }

    public async Task<string> UploadAsync(string filePath, string objectKey, string contentType)
    {
        using var activity = ActivitySources.Storage.StartActivity("minio.upload");

        activity?.SetTag("object.key", objectKey);
        activity?.SetTag("bucket", _options.Bucket);

        await EnsureBucketAsync(_options.Bucket);

        await _minio.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_options.Bucket)
            .WithObject(objectKey)
            .WithFileName(filePath)
            .WithContentType(contentType));

        var url = BuildPublicUrl(objectKey);

        activity?.SetTag("object.url", url);

        return url;
    }

    public async Task DeleteAsync(string objectKey)
    {
        await DeleteAsync(StorageBucket.Images, objectKey);
    }

    public async Task EnsureBucketsAsync(CancellationToken ct = default)
    {
        await EnsureBucketAsync(_options.Bucket, ct);
        await EnsureBucketAsync(_options.ModelBucket, ct);
    }

    public async Task<string> UploadStreamAsync(
        StorageBucket bucket,
        string objectKey,
        Stream content,
        long size,
        string contentType,
        CancellationToken ct = default)
    {
        using var activity = ActivitySources.Storage.StartActivity("minio.upload");

        var bucketName = BucketName(bucket);

        activity?.SetTag("object.key", objectKey);
        activity?.SetTag("bucket", bucketName);

        await EnsureBucketAsync(bucketName, ct);

        await _minio.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectKey)
            .WithStreamData(content)
            .WithObjectSize(size)
            .WithContentType(contentType), ct);

        return BuildPublicUrl(bucketName, objectKey);
    }

    public async Task DeleteAsync(StorageBucket bucket, string objectKey, CancellationToken ct = default)
    {
        using var activity = ActivitySources.Storage.StartActivity("minio.delete");

        activity?.SetTag("object.key", objectKey);

        await _minio.RemoveObjectAsync(new RemoveObjectArgs()
            .WithBucket(BucketName(bucket))
            .WithObject(objectKey), ct);
    }

    public async Task<string> GetPresignedUrlAsync(
        StorageBucket bucket,
        string objectKey,
        TimeSpan lifetime,
        CancellationToken ct = default)
    {
        var seconds = (int)Math.Clamp(lifetime.TotalSeconds, 1, 7 * 24 * 3600);

        return await _presignClient.Value.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(BucketName(bucket))
            .WithObject(objectKey)
            .WithExpiry(seconds));
    }

    public async Task<IReadOnlyList<StoredObject>> ListObjectsAsync(
        StorageBucket bucket,
        CancellationToken ct = default)
    {
        var bucketName = BucketName(bucket);

        if (!await _minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName), ct))
            return [];

        var objects = new List<StoredObject>();

        await foreach (var item in _minio.ListObjectsEnumAsync(
                           new ListObjectsArgs().WithBucket(bucketName).WithRecursive(true), ct))
        {
            if (item.IsDir)
                continue;

            var modified = item.LastModifiedDateTime ?? DateTime.UtcNow;
            objects.Add(new StoredObject(item.Key, DateTime.SpecifyKind(modified, DateTimeKind.Utc)));
        }

        return objects;
    }

    private string BucketName(StorageBucket bucket) =>
        bucket == StorageBucket.Models ? _options.ModelBucket : _options.Bucket;

    private async Task EnsureBucketAsync(string bucketName, CancellationToken ct = default)
    {
        var exists = await _minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName), ct);

        if (!exists)
            await _minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName), ct);
    }

    private IMinioClient BuildPresignClient()
    {
        var endpoint = NormalizeBaseUrl(_options.PresignEndpoint ?? _options.PublicEndpoint ?? _options.Endpoint);
        var uri = new Uri(endpoint);

        return new MinioClient()
            .WithEndpoint(uri.Authority)
            .WithCredentials(_options.AccessKey, _options.SecretKey)
            .WithSSL(uri.Scheme == Uri.UriSchemeHttps)
            .WithRegion(PresignRegion)
            .Build();
    }

    private string BuildPublicUrl(string objectKey) => BuildPublicUrl(_options.Bucket, objectKey);

    private string BuildPublicUrl(string bucketName, string objectKey)
    {
        var baseUrl = NormalizeBaseUrl(_options.PublicEndpoint ?? _options.Endpoint);
        return $"{baseUrl}/{bucketName}/{objectKey}";
    }

    private static string NormalizeBaseUrl(string url)
    {
        var baseUrl = url.TrimEnd('/');

        if (!baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            baseUrl = $"http://{baseUrl}";
        }

        return baseUrl;
    }
}
