namespace Infrastructure.Configuration;

public class RedisOptions
{
    public string ConnectionString { get; set; } = default!;
    public int DefaultTtlSeconds { get; set; } = 300;
}