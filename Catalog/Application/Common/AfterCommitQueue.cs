using Infrastructure.Abstractions.Caching;
using Microsoft.Extensions.Logging;

namespace Application.Common;

public interface IAfterCommit
{
    void Register(Func<CancellationToken, Task> action);

    Task RunAsync(CancellationToken ct);

    void Clear();
}

public sealed class AfterCommitQueue(ILogger<AfterCommitQueue> logger) : IAfterCommit
{
    private readonly List<Func<CancellationToken, Task>> _actions = [];

    public void Register(Func<CancellationToken, Task> action) => _actions.Add(action);

    public async Task RunAsync(CancellationToken ct)
    {
        var pending = _actions.ToArray();
        _actions.Clear();

        foreach (var action in pending)
        {
            try
            {
                await action(ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "A post-commit step failed");
            }
        }
    }

    public void Clear() => _actions.Clear();
}

public class ProductCacheInvalidator(ICacheService cache, IAfterCommit afterCommit)
{
    public async Task InvalidateAsync(Guid? productId = null)
    {
        await RemoveAsync(productId, CancellationToken.None);

        afterCommit.Register(ct => RemoveAsync(productId, ct));
    }

    public async Task InvalidateAllAsync()
    {
        await RemoveAllAsync(CancellationToken.None);

        afterCommit.Register(RemoveAllAsync);
    }

    private async Task RemoveAllAsync(CancellationToken _)
    {
        await cache.RemoveByPrefixAsync("product:");
        await cache.RemoveByPrefixAsync("products:");
    }

    private async Task RemoveAsync(Guid? productId, CancellationToken _)
    {
        if (productId.HasValue)
            await cache.RemoveAsync($"product:{productId.Value}");

        await cache.RemoveByPrefixAsync("products:");
    }
}
