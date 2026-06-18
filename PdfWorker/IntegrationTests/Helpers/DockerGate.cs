using Docker.DotNet;

namespace IntegrationTests.Helpers;

/// <summary>
/// Cached check so integration tests can skip cleanly when Docker is not running (e.g. CI/agent).
/// </summary>
public static class DockerGate
{
    private static readonly Lazy<bool> LazyAvailable = new(() =>
    {
        try
        {
            using var cfg = new DockerClientConfiguration();
            using var client = cfg.CreateClient();
            client.System.PingAsync(CancellationToken.None).GetAwaiter().GetResult();
            return true;
        }
        catch
        {
            return false;
        }
    });

    public static bool IsAvailable() => LazyAvailable.Value;
}
