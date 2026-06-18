using Application.Contracts;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Infrastructure.Rendering.Templates;

public static class ReceiptDocumentV1
{
    public static IDocument Create(ReceiptDocumentRequestV1 request) =>
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Text($"Payment Receipt {request.ReceiptNumber}").FontSize(18).Bold();
                page.Content().Column(col =>
                {
                    col.Item().Text($"Paid at: {request.PaidAt:yyyy-MM-dd HH:mm} UTC");
                    col.Item().PaddingTop(10).Text(request.Seller.LegalName);
                    col.Item().Text($"Customer: {request.Customer.Name} ({request.Customer.Email})");
                    col.Item().PaddingTop(10).Text($"Amount paid: {request.AmountPaid.Amount:N2} {request.AmountPaid.Currency}").Bold();
                    col.Item().Text($"Payment method: {request.PaymentMethodSummary}");
                    col.Item().Text($"Order ID: {request.OrderId}");
                    col.Item().Text($"Payment ID: {request.PaymentId}");
                    col.Item().Text($"Provider ref: {request.ProviderPaymentId}");
                });
            });
        });
}
