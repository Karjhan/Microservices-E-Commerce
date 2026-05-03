using System.Text.Json;
using Infrastructure.Abstractions.Caching;
using Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Infrastructure.Caching;

public class RedisCacheService : ICacheService
{
    private readonly IDatabase _db;
    private readonly RedisOptions _options;

    public RedisCacheService(
        IConnectionMultiplexer multiplexer,
        IOptions<RedisOptions> options)
    {
        _db = multiplexer.GetDatabase();
        _options = options.Value;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await _db.StringGetAsync(key);

        if (value.IsNullOrEmpty)
            return default;

        return JsonSerializer.Deserialize<T>(value.ToString());
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null)
    {
        var json = JsonSerializer.Serialize(value);

        await _db.StringSetAsync(
            key,
            json,
            ttl ?? TimeSpan.FromSeconds(_options.DefaultTtlSeconds));
    }

    public async Task RemoveAsync(string key)
    {
        await _db.KeyDeleteAsync(key);
    }

    public async Task RemoveByPrefixAsync(string prefix)
    {
        var server = GetServer();

        var keys = server.Keys(pattern: $"{prefix}*").ToArray();

        foreach (var key in keys)
        {
            await _db.KeyDeleteAsync(key);
        }
    }

    private IServer GetServer()
    {
        var endpoint = _db.Multiplexer.GetEndPoints().First();
        return _db.Multiplexer.GetServer(endpoint);
    }
}