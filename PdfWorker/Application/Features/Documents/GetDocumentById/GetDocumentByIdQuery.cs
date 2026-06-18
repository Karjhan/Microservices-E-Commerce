using Application.Abstractions.Persistence;
using Application.Features.Documents.ListDocuments;
using Domain.Exceptions;
using MediatR;

namespace Application.Features.Documents.GetDocumentById;

public record GetDocumentByIdQuery(Guid DocumentId) : IRequest<DocumentDto>;

public class GetDocumentByIdHandler : IRequestHandler<GetDocumentByIdQuery, DocumentDto>
{
    private readonly IDocumentRepository _repository;

    public GetDocumentByIdHandler(IDocumentRepository repository)
    {
        _repository = repository;
    }

    public async Task<DocumentDto> Handle(GetDocumentByIdQuery query, CancellationToken cancellationToken)
    {
        var document = await _repository.GetByIdAsync(query.DocumentId, cancellationToken)
            ?? throw new DocumentNotFoundException(query.DocumentId);

        return ListDocumentsHandler.Map(document);
    }
}
