using Testcontainers.PostgreSql;

namespace IntegrationTests.Common;

public static class PostgresContainer
{
    public static PostgreSqlContainer Instance { get; } =
        new PostgreSqlBuilder()
            .WithImage("postgres:17")
            .WithDatabase("auth_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
}