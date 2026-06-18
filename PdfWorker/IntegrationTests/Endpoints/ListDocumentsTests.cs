using System.Net;
using FluentAssertions;
using IntegrationTests.Collection;
using IntegrationTests.Helpers;

namespace IntegrationTests.Endpoints;

[Collection("PdfWorker")]
public class ListDocumentsTests
{
    private readonly PdfWorkerApiFactory _factory;

    public ListDocumentsTests(PdfWorkerApiFactory factory)
    {
        _factory = factory;
    }

    [SkippableFact]
    public async Task Should_Return_BadRequest_When_No_Order_Or_Payment()
    {
        Skip.IfNot(DockerGate.IsAvailable());

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/documents");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [SkippableFact]
    public async Task Should_Return_Documents_For_Order()
    {
        Skip.IfNot(DockerGate.IsAvailable());

        var client = _factory.CreateClient();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await _factory.SeedCompletedDocumentAsync(orderId, paymentId, userId);

        var response = await client.GetAsync($"/documents?orderId={orderId}");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain(orderId.ToString());
        json.Should().Contain("\"totalCount\":1");
    }
}
