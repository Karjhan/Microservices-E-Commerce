using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Application;

public static class CacheKeyGenerator
{
    public static string ForProducts(object filter)
    {
        return $"products:{ComputeHash(filter)}";
    }

    private static string ComputeHash(object obj)
    {
        var json = JsonSerializer.Serialize(obj);

        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(json));

        return Convert.ToHexString(bytes);
    }
}