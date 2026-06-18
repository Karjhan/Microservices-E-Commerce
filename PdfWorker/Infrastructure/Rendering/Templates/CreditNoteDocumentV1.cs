using Application.Contracts;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Infrastructure.Rendering.Templates;

public static class CreditNoteDocumentV1
{
    public static IDocument Create(CreditNoteDocumentRequestV1 request) =>
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Text($"Credit Note {request.CreditNoteNumber}").FontSize(18).Bold();
                page.Content().Column(col =>
                {
                    col.Item().Text($"Issue date: {request.IssueDate:yyyy-MM-dd HH:mm} UTC");
                    col.Item().PaddingTop(10).Text(request.Seller.LegalName);
                    col.Item().Text($"Customer: {request.Customer.Name} ({request.Customer.Email})");
                    col.Item().PaddingTop(10).Text($"Refund amount: {FormatMoney(request.RefundAmount)}").Bold();
                    col.Item().Text($"Cumulative refunded: {FormatMoney(request.CumulativeRefunded)}");
                    col.Item().Text($"Remaining balance: {FormatMoney(request.RemainingBalance)}");
                    col.Item().Text($"Original payment: {FormatMoney(request.OriginalPaymentAmount)}");

                    if (request.LineItems.Count > 0)
                    {
                        col.Item().PaddingTop(10).Text("Refunded items").Bold();
                        foreach (var item in request.LineItems)
                        {
                            col.Item().Text($"{item.Description} x{item.Quantity}: {FormatMoney(item.LineTotal)}");
                        }
                    }
                });
            });
        });

    private static string FormatMoney(MoneyV1 money) =>
        $"{money.Amount:N2} {money.Currency}";
}
