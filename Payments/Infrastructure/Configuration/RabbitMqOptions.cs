namespace Infrastructure.Configuration;

public class RabbitMqOptions
{
    public string Host { get; set; } = default!;
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string VirtualHost { get; set; } = "/";
}
