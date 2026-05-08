using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using DotNet.Testcontainers.Networks;

namespace IntegrationTests.Fixtures;

public class GraphRagFixture : IAsyncLifetime
{
    private readonly INetwork _network;

    public IContainer Container { get; private set; } = null!;
    public string BaseUrl => $"http://localhost:{Container.GetMappedPublicPort(8100)}";

    public GraphRagFixture(INetwork network)
    {
        _network = network;
    }

    public async Task InitializeAsync()
    {
        Container = new ContainerBuilder()
            .WithImage("ecommerce-infra-graphrag-api:latest")
            .WithImagePullPolicy(PullPolicy.Never)
            .WithNetwork(_network)
            .WithNetworkAliases("graphrag-api")
            .WithPortBinding(8100, true)

            .WithEnvironment("SERVICE_MODE", "api")

            .WithEnvironment("QDRANT_HOST", "qdrant")
            .WithEnvironment("QDRANT_PORT", "6333")
            .WithEnvironment("QDRANT_COLLECTION", "products")

            .WithEnvironment("NEO4J_URI", "bolt://neo4j:7687")
            .WithEnvironment("NEO4J_USER", "neo4j")
            .WithEnvironment("NEO4J_PASSWORD", "neo4jpassword")

            .WithEnvironment("RABBITMQ_URL", "amqp://guest:guest@rabbitmq:5672/")
            .WithEnvironment("RABBITMQ_EXCHANGE", "catalog-exchange")
            .WithEnvironment("RABBITMQ_QUEUE", "graphrag-ingestion")

            .WithEnvironment("OLLAMA_BASE_URL", "http://ollama:11434")
            .WithEnvironment("EMBEDDING_MODEL", "bge-m3")
            .WithEnvironment("RERANKER_MODEL", "bge-reranker-v2-m3")
            .WithEnvironment("GENERATION_MODEL", "gemma3:4b")
            .WithEnvironment("GENERATION_MODEL_LARGE", "gemma3:12b")

            .WithEnvironment("OTEL_ENABLED", "false")
            .WithEnvironment("LOG_LEVEL", "INFO")

            .WithEntrypoint("bash", "/app/docker/entrypoint.sh")

            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(r => r.ForPort(8100).ForPath("/health"))
            )
            .Build();

        await Container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await Container.DisposeAsync();
    }
}