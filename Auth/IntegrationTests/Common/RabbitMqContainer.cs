using Testcontainers.RabbitMq;

namespace IntegrationTests.Common;

public static class RabbitMqTestContainer
{
    public static RabbitMqContainer Instance { get; } =
        new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management")
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();
}