using System.Net;
using FluentAssertions;
using IntegrationTests.Collection;
using IntegrationTests.Helpers;

namespace IntegrationTests.Endpoints;

[Collection("PdfWorker")]
public class GetDocumentByIdTests
{
    private readonly PdfWorkerApiFactory _factory;

    public GetDocumentByIdTests(PdfWorkerApiFactory factory)
    {
        _factory = factory;
    }

    [SkippableFact]
    public async Task Should_Return_NotFound_For_Unknown_Id()
    {
        Skip.IfNot(DockerGate.IsAvailable());

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/documents/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [SkippableFact]
    public async Task Should_Return_Document_When_Exists()
    {
        Skip.IfNot(DockerGate.IsAvailable());

        var client = _factory.CreateClient();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var id = await _factory.SeedCompletedDocumentAsync(orderId, paymentId, userId);

        var response = await client.GetAsync($"/documents/{id}");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain(id.ToString());
        json.Should().Contain("Invoice");
    }
}
