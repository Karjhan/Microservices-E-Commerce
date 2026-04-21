using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Domain.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Infrastructure.Messaging;

public class RabbitMqPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly ILogger<RabbitMqPublisher> _logger;
    private readonly ConnectionFactory _factory;
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly string _exchange;
    private static readonly ActivitySource ActivitySource = new("AuthService.RabbitMQ");

    public RabbitMqPublisher(IConfiguration config, ILogger<RabbitMqPublisher> logger)
    {
        _logger = logger;

        _factory = new ConnectionFactory
        {
            HostName = config["RabbitMq:Host"],
            Port = int.Parse(config["RabbitMq:Port"]!),
            UserName = config["RabbitMq:Username"],
            Password = config["RabbitMq:Password"]
        };

        _exchange = config["RabbitMq:Exchange"]!;
    }

    private async Task EnsureConnectedAsync()
    {
        if (_connection != null && _channel != null)
            return;

        _connection = await _factory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

        await _channel.ExchangeDeclareAsync(
            exchange: _exchange,
            type: ExchangeType.Topic,
            durable: true
        );

        _logger.LogInformation("RabbitMQ connected");
    }

    public async Task PublishAsync<T>(T @event, string routingKey, CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("RabbitMQ Publish");

        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination", routingKey);
        activity?.SetTag("messaging.operation", "publish");
        
        await EnsureConnectedAsync();

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event));

        var props = new BasicProperties
        {
            Persistent = true
        };

        await _channel!.BasicPublishAsync(
            exchange: _exchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: props,
            body: body
        );

        _logger.LogInformation("Published event {Event} with routing key {RoutingKey}",
            typeof(T).Name, routingKey);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel != null)
            await _channel.DisposeAsync();

        if (_connection != null)
            await _connection.DisposeAsync();
    }
}