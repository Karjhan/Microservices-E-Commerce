using Application.Abstractions.Rendering;
using Application.Contracts;
using Domain.Exceptions;
using Infrastructure.Rendering.Templates;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Infrastructure.Rendering;

public class QuestPdfRenderer : IPdfRenderer
{
    static QuestPdfRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<byte[]> RenderAsync(string templateVersion, object request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var document = templateVersion switch
        {
            "invoice-v1" when request is InvoiceDocumentRequestV1 invoice =>
                InvoiceDocumentV1.Create(invoice),
            "receipt-v1" when request is ReceiptDocumentRequestV1 receipt =>
                ReceiptDocumentV1.Create(receipt),
            "credit-note-v1" when request is CreditNoteDocumentRequestV1 creditNote =>
                CreditNoteDocumentV1.Create(creditNote),
            _ => throw new DocumentRenderException($"Unknown template version: {templateVersion}")
        };

        var bytes = document.GeneratePdf();
        return Task.FromResult(bytes);
    }
}
