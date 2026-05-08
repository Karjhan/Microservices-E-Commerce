using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;

namespace IntegrationTests.Fixtures;

public class TestEnvironment : IAsyncLifetime
{
    public INetwork Network { get; private set; } = null!;

    public PostgresFixture Postgres { get; private set; } = null!;
    public RabbitMqFixture RabbitMq { get; private set; } = null!;
    public RedisFixture Redis { get; private set; } = null!;
    public MinioFixture Minio { get; private set; } = null!;
    public QdrantFixture Qdrant { get; private set; } = null!;
    public Neo4jFixture Neo4j { get; private set; } = null!;
    public OllamaFixture Ollama { get; private set; } = null!;
    public AuthFixture Auth { get; private set; } = null!;
    public CatalogFixture Catalog { get; private set; } = null!;
    public GraphRagFixture GraphRag { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Network = new NetworkBuilder()
            .WithName($"test-network-{Guid.NewGuid()}")
            .Build();

        await Network.CreateAsync();

        Postgres = new PostgresFixture(Network);
        await Postgres.InitializeAsync();

        RabbitMq = new RabbitMqFixture(Network);
        await RabbitMq.InitializeAsync();

        Redis = new RedisFixture(Network);
        await Redis.InitializeAsync();

        Minio = new MinioFixture(Network);
        await Minio.InitializeAsync();

        Qdrant = new QdrantFixture(Network);
        await Qdrant.InitializeAsync();

        Neo4j = new Neo4jFixture(Network);
        await Neo4j.InitializeAsync();

        Ollama = new OllamaFixture(Network);
        await Ollama.InitializeAsync();

        Auth = new AuthFixture(Network);
        await Auth.InitializeAsync();

        Catalog = new CatalogFixture(Network);
        await Catalog.InitializeAsync();

        GraphRag = new GraphRagFixture(Network);
        await GraphRag.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await GraphRag.DisposeAsync();
        await Catalog.DisposeAsync();
        await Auth.DisposeAsync();
        await Ollama.DisposeAsync();
        await Neo4j.DisposeAsync();
        await Qdrant.DisposeAsync();
        await Minio.DisposeAsync();
        await Redis.DisposeAsync();
        await RabbitMq.DisposeAsync();
        await Postgres.DisposeAsync();
        await Network.DeleteAsync();
    }
}