using Application.Abstractions.Persistence;
using Application.Features.Documents.GetDocumentById;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace UnitTests.Application;

public class GetDocumentByIdHandlerTests
{
    private readonly Mock<IDocumentRepository> _repository = new();
    private readonly GetDocumentByIdHandler _handler;

    public GetDocumentByIdHandlerTests()
    {
        _handler = new GetDocumentByIdHandler(_repository.Object);
    }

    [Fact]
    public async Task Should_Return_Document_When_Found()
    {
        var id = Guid.NewGuid();
        var doc = Document.CreatePending(
            DocumentType.Receipt,
            "receipt:k",
            "receipt-v1",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "payment.completed",
            null,
            null);

        _repository.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        var result = await _handler.Handle(new GetDocumentByIdQuery(id), CancellationToken.None);

        result.Id.Should().Be(doc.Id);
        result.DocumentType.Should().Be(DocumentType.Receipt);
    }

    [Fact]
    public async Task Should_Throw_When_Not_Found()
    {
        var id = Guid.NewGuid();
        _repository.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Document?)null);

        var act = () => _handler.Handle(new GetDocumentByIdQuery(id), CancellationToken.None);

        await act.Should().ThrowAsync<DocumentNotFoundException>();
    }
}
