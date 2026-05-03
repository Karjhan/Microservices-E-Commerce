using Application.Abstractions.Storage;
using Contracts;
using Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace Infrastructure.Storage;

public class MinioFileStorageService : IFileStorageService
{
    private readonly IMinioClient _minio;
    private readonly MinioOptions _options;

    public MinioFileStorageService(
        IMinioClient minio,
        IOptions<MinioOptions> options)
    {
        _minio = minio;
        _options = options.Value;
    }

    public async Task<string> UploadAsync(string filePath, string objectKey, string contentType)
    {
        using var activity = ActivitySources.Storage.StartActivity("minio.upload");

        activity?.SetTag("object.key", objectKey);
        activity?.SetTag("bucket", _options.Bucket);

        var bucketExists = await _minio.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(_options.Bucket));

        if (!bucketExists)
        {
            await _minio.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(_options.Bucket));
        }

        await _minio.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_options.Bucket)
            .WithObject(objectKey)
            .WithFileName(filePath)
            .WithContentType(contentType));

        var url = $"{_options.Endpoint}/{_options.Bucket}/{objectKey}";

        activity?.SetTag("object.url", url);

        return url;
    }

    public async Task DeleteAsync(string objectKey)
    {
        using var activity = ActivitySources.Storage.StartActivity("minio.delete");

        activity?.SetTag("object.key", objectKey);

        await _minio.RemoveObjectAsync(new RemoveObjectArgs()
            .WithBucket(_options.Bucket)
            .WithObject(objectKey));
    }
}