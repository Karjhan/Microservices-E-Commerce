using Application.Abstractions.Persistence;
using Application.Features.Documents.ListDocuments;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace UnitTests.Application;

public class ListDocumentsHandlerTests
{
    private readonly Mock<IDocumentRepository> _repository = new();
    private readonly ListDocumentsHandler _handler;

    public ListDocumentsHandlerTests()
    {
        _handler = new ListDocumentsHandler(_repository.Object);
    }

    [Fact]
    public async Task Should_Throw_When_Order_And_Payment_Missing()
    {
        var act = () => _handler.Handle(
            new ListDocumentsQuery(null, null, null, null, 1, 20),
            CancellationToken.None);

        await act.Should().ThrowAsync<DocumentValidationException>()
            .WithMessage("At least one of orderId or paymentId is required");
    }

    [Fact]
    public async Task Should_Return_Empty_When_No_Rows()
    {
        _repository
            .Setup(x => x.ListAsync(
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<DocumentType?>(),
                It.IsAny<GenerationStatus?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<Document>(), 0));

        var orderId = Guid.NewGuid();
        var result = await _handler.Handle(
            new ListDocumentsQuery(orderId, null, null, null, 1, 20),
            CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task Should_Map_Repository_Items()
    {
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var doc = Document.CreatePending(
            DocumentType.Invoice,
            "invoice:k1",
            "invoice-v1",
            orderId,
            paymentId,
            userId,
            "payment.completed",
            "corr-1",
            "{}");
        doc.MarkCompleted("documents", "documents/invoice/x.pdf", "application/pdf", 42);

        _repository
            .Setup(x => x.ListAsync(
                orderId,
                null,
                null,
                null,
                1,
                20,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(([doc], 1));

        var result = await _handler.Handle(
            new ListDocumentsQuery(orderId, null, null, null, 1, 20),
            CancellationToken.None);

        result.Items.Should().ContainSingle();
        result.Items[0].Id.Should().Be(doc.Id);
        result.Items[0].DocumentType.Should().Be(DocumentType.Invoice);
        result.Items[0].Status.Should().Be(GenerationStatus.Completed);
        result.Items[0].StorageObjectKey.Should().Be("documents/invoice/x.pdf");
        result.TotalCount.Should().Be(1);
    }
}
