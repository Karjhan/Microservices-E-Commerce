using System.Security.Cryptography;
using System.Text;
using Application.Abstractions.Storage;
using PdfWorker.Contracts;
using Domain.Exceptions;
using Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace Infrastructure.Storage;

public class MinioObjectStorageService : IObjectStorageService
{
    private readonly IMinioClient _minio;
    private readonly MinioOptions _options;

    public MinioObjectStorageService(IMinioClient minio, IOptions<MinioOptions> options)
    {
        _minio = minio;
        _options = options.Value;
    }

    public async Task<(string Bucket, string ObjectKey, long FileSizeBytes)> UploadAsync(
        byte[] content,
        string documentType,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySources.Storage.StartActivity("minio.upload");

        try
        {
            var bucketExists = await _minio.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(_options.Bucket),
                cancellationToken);

            if (!bucketExists)
            {
                await _minio.MakeBucketAsync(
                    new MakeBucketArgs().WithBucket(_options.Bucket),
                    cancellationToken);
            }

            var hash = Convert.ToHexString(SHA256.HashData(content))[..16].ToLowerInvariant();
            var now = DateTime.UtcNow;
            var objectKey = $"documents/{documentType.ToLowerInvariant()}/{now:yyyy}/{now:MM}/{hash}.pdf";

            activity?.SetTag("object.key", objectKey);
            activity?.SetTag("bucket", _options.Bucket);

            using var stream = new MemoryStream(content);

            await _minio.PutObjectAsync(new PutObjectArgs()
                .WithBucket(_options.Bucket)
                .WithObject(objectKey)
                .WithStreamData(stream)
                .WithObjectSize(content.LongLength)
                .WithContentType(contentType),
                cancellationToken);

            return (_options.Bucket, objectKey, content.LongLength);
        }
        catch (Exception ex)
        {
            throw new DocumentStorageException($"MinIO upload failed: {ex.Message}", isTransient: true);
        }
    }
}
