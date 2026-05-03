using Infrastructure.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace Catalog.Endpoints.Health;

public class MinioHealthCheck : IHealthCheck
{
    private readonly IMinioClient _minio;
    private readonly MinioOptions _options;

    public MinioHealthCheck(IMinioClient minio, IOptions<MinioOptions> options)
    {
        _minio = minio;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var exists = await _minio.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(_options.Bucket),
                cancellationToken);

            return exists
                ? HealthCheckResult.Healthy("MinIO bucket reachable")
                : HealthCheckResult.Unhealthy("Bucket does not exist");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MinIO unreachable", ex);
        }
    }
}