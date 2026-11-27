using Application.Abstractions.Storage;
using Infrastructure.Abstractions.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundJobs;

public class OrphanedObjectSweepJob(
    IServiceScopeFactory scopeFactory,
    ILogger<OrphanedObjectSweepJob> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    public static readonly TimeSpan GracePeriod = TimeSpan.FromHours(1);

    public const string ImagePrefix = "products/";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        using var timer = new PeriodicTimer(Interval);

        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var removed = await SweepAsync(
                    scope.ServiceProvider.GetRequiredService<IProductRepository>(),
                    scope.ServiceProvider.GetRequiredService<IFileStorageService>(),
                    DateTime.UtcNow,
                    stoppingToken);

                if (removed > 0)
                    logger.LogInformation("Orphan sweep removed {Count} object(s)", removed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Orphan sweep failed; it will retry on the next run");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public static async Task<int> SweepAsync(
        IProductRepository repo,
        IFileStorageService storage,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var (imageKeys, modelKeys) = await repo.GetReferencedObjectKeysAsync(ct);
        var cutoff = nowUtc - GracePeriod;
        var removed = 0;

        foreach (var item in await storage.ListObjectsAsync(StorageBucket.Images, ct))
        {
            if (item.Key.StartsWith(ImagePrefix, StringComparison.Ordinal)
                && item.LastModifiedUtc < cutoff
                && !imageKeys.Contains(item.Key))
            {
                await storage.DeleteAsync(StorageBucket.Images, item.Key, ct);
                removed++;
            }
        }

        foreach (var item in await storage.ListObjectsAsync(StorageBucket.Models, ct))
        {
            if (item.LastModifiedUtc < cutoff && !modelKeys.Contains(item.Key))
            {
                await storage.DeleteAsync(StorageBucket.Models, item.Key, ct);
                removed++;
            }
        }

        return removed;
    }
}
