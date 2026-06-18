using Application.Features.Documents.GetDocumentById;
using Application.Features.Documents.ListDocuments;
using Domain.Enums;
using MediatR;

namespace PdfWorker.Endpoints;

public static class ListDocumentsEndpoint
{
    public static IEndpointRouteBuilder MapListDocuments(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", async (
            Guid? orderId,
            Guid? paymentId,
            DocumentType? documentType,
            GenerationStatus? status,
            int page,
            int pageSize,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (orderId is null && paymentId is null)
            {
                return Results.BadRequest(new
                {
                    title = "At least one of orderId or paymentId is required",
                    status = 400,
                    detail = "Provide orderId and/or paymentId query parameters"
                });
            }

            var result = await mediator.Send(new ListDocumentsQuery(
                orderId,
                paymentId,
                documentType,
                status,
                page <= 0 ? 1 : page,
                pageSize <= 0 ? 20 : Math.Min(pageSize, 100)), ct);

            return Results.Ok(result);
        });

        return app;
    }
}

public static class GetDocumentByIdEndpoint
{
    public static IEndpointRouteBuilder MapGetDocumentById(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{documentId:guid}", async (
            Guid documentId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetDocumentByIdQuery(documentId), ct);
            return Results.Ok(result);
        });

        return app;
    }
}
