using Application.Contracts;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Infrastructure.Rendering.Templates;

public static class InvoiceDocumentV1
{
    public static IDocument Create(InvoiceDocumentRequestV1 request) =>
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Text($"Invoice {request.DocumentNumber}").FontSize(18).Bold();
                page.Content().Column(col =>
                {
                    col.Item().Text($"Issue date: {request.IssueDate:yyyy-MM-dd HH:mm} UTC");
                    col.Item().PaddingTop(10).Text("Seller").Bold();
                    col.Item().Text(request.Seller.LegalName);
                    col.Item().Text($"{request.Seller.AddressLine1}, {request.Seller.PostalCode} {request.Seller.City}");
                    col.Item().Text($"VAT: {request.Seller.VatId}");

                    col.Item().PaddingTop(10).Text("Customer").Bold();
                    col.Item().Text(request.Customer.Name);
                    col.Item().Text(request.Customer.Email);
                    col.Item().Text($"{request.Customer.AddressLine1}, {request.Customer.PostalCode} {request.Customer.City}");

                    col.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn();
                        });

                        table.Header(h =>
                        {
                            h.Cell().Text("Description").Bold();
                            h.Cell().Text("Qty").Bold();
                            h.Cell().Text("Unit").Bold();
                            h.Cell().Text("Total").Bold();
                        });

                        foreach (var item in request.LineItems)
                        {
                            table.Cell().Text(item.Description);
                            table.Cell().Text(item.Quantity.ToString());
                            table.Cell().Text(FormatMoney(item.UnitPrice));
                            table.Cell().Text(FormatMoney(item.LineTotal));
                        }
                    });

                    col.Item().PaddingTop(10).AlignRight().Column(totals =>
                    {
                        totals.Item().Text($"Subtotal: {FormatMoney(request.Subtotal)}");
                        totals.Item().Text($"Tax ({request.TaxRatePercent}%): {FormatMoney(request.Tax)}");
                        totals.Item().Text($"Total: {FormatMoney(request.Total)}").Bold();
                    });
                });
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Payment ref: ");
                    x.Span(request.ProviderPaymentId);
                });
            });
        });

    private static string FormatMoney(MoneyV1 money) =>
        $"{money.Amount:N2} {money.Currency}";
}
