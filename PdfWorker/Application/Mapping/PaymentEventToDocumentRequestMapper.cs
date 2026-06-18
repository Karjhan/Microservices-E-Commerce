using Application.Abstractions.Enrichment;
using Application.Contracts;
using Contracts.Events;
using Microsoft.Extensions.Options;

namespace Application.Mapping;

public class SellerOptions
{
    public string LegalName { get; set; } = default!;
    public string VatId { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string AddressLine1 { get; set; } = default!;
    public string City { get; set; } = default!;
    public string PostalCode { get; set; } = default!;
    public string Country { get; set; } = default!;
}

public class PaymentEventToDocumentRequestMapper
{
    private readonly SellerOptions _seller;

    public PaymentEventToDocumentRequestMapper(IOptions<SellerOptions> sellerOptions)
    {
        _seller = sellerOptions.Value;
    }

    public InvoiceDocumentRequestV1 MapToInvoice(PaymentCompleted payment, OrderEnrichmentDto? order)
    {
        var seller = BuildSeller();
        var customer = BuildCustomer(payment.RecipientEmail, order);
        var lineItems = BuildLineItems(order, payment.Currency, payment.Amount);
        var subtotal = CalculateSubtotal(lineItems, payment.Amount, payment.Currency);

        return new InvoiceDocumentRequestV1
        {
            DocumentNumber = BuildDocumentNumber("INV", payment.PaymentId),
            IssueDate = payment.CompletedAt,
            OrderId = payment.OrderId,
            PaymentId = payment.PaymentId,
            ProviderPaymentId = payment.ProviderPaymentId,
            Seller = seller,
            Customer = customer,
            LineItems = lineItems,
            Subtotal = subtotal,
            Tax = new MoneyV1 { Amount = payment.Amount - subtotal.Amount, Currency = payment.Currency },
            TaxRatePercent = 19m,
            Total = new MoneyV1 { Amount = payment.Amount, Currency = payment.Currency }
        };
    }

    public ReceiptDocumentRequestV1 MapToReceipt(PaymentCompleted payment, OrderEnrichmentDto? order)
    {
        return new ReceiptDocumentRequestV1
        {
            ReceiptNumber = BuildDocumentNumber("RCP", payment.PaymentId),
            IssueDate = payment.CompletedAt,
            OrderId = payment.OrderId,
            PaymentId = payment.PaymentId,
            ProviderPaymentId = payment.ProviderPaymentId,
            Seller = BuildSeller(),
            Customer = BuildCustomer(payment.RecipientEmail, order),
            AmountPaid = new MoneyV1 { Amount = payment.Amount, Currency = payment.Currency },
            PaidAt = payment.CompletedAt
        };
    }

    public CreditNoteDocumentRequestV1 MapToCreditNote(PaymentRefunded payment, OrderEnrichmentDto? order)
    {
        var remaining = payment.OriginalAmount - payment.TotalRefunded;
        var lineItems = payment.IsFullRefund
            ? []
            : BuildLineItems(order, payment.Currency, payment.RefundAmount);

        return new CreditNoteDocumentRequestV1
        {
            CreditNoteNumber = BuildDocumentNumber("CN", payment.PaymentId),
            IssueDate = payment.RefundedAt,
            OrderId = payment.OrderId,
            PaymentId = payment.PaymentId,
            OriginalPaymentAmount = new MoneyV1 { Amount = payment.OriginalAmount, Currency = payment.Currency },
            RefundAmount = new MoneyV1 { Amount = payment.RefundAmount, Currency = payment.Currency },
            CumulativeRefunded = new MoneyV1 { Amount = payment.TotalRefunded, Currency = payment.Currency },
            RemainingBalance = new MoneyV1 { Amount = remaining, Currency = payment.Currency },
            IsFullRefund = payment.IsFullRefund,
            Seller = BuildSeller(),
            Customer = BuildCustomer(payment.RecipientEmail, order),
            LineItems = lineItems
        };
    }

    private SellerV1 BuildSeller() => new()
    {
        LegalName = _seller.LegalName,
        VatId = _seller.VatId,
        Email = _seller.Email,
        AddressLine1 = _seller.AddressLine1,
        City = _seller.City,
        PostalCode = _seller.PostalCode,
        Country = _seller.Country
    };

    private static PartyV1 BuildCustomer(string? email, OrderEnrichmentDto? order)
    {
        var address = order?.ShippingAddress;
        return new PartyV1
        {
            Name = address?.FullName ?? "Customer",
            Email = email ?? order?.RecipientEmail ?? "unknown@example.com",
            AddressLine1 = address?.Street,
            City = address?.City,
            State = address?.State,
            PostalCode = address?.ZipCode,
            Country = address?.Country
        };
    }

    private static List<LineItemV1> BuildLineItems(OrderEnrichmentDto? order, string currency, decimal fallbackTotal)
    {
        if (order?.Items is { Count: > 0 } items)
        {
            return items.Select(i => new LineItemV1
            {
                ProductId = i.ProductId,
                Description = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = new MoneyV1 { Amount = i.UnitPrice, Currency = i.Currency },
                LineTotal = new MoneyV1 { Amount = i.LineTotal, Currency = i.Currency }
            }).ToList();
        }

        return
        [
            new LineItemV1
            {
                ProductId = Guid.Empty,
                Description = "Order items",
                Quantity = 1,
                UnitPrice = new MoneyV1 { Amount = fallbackTotal, Currency = currency },
                LineTotal = new MoneyV1 { Amount = fallbackTotal, Currency = currency }
            }
        ];
    }

    private static MoneyV1 CalculateSubtotal(List<LineItemV1> lineItems, decimal paymentAmount, string currency)
    {
        var subtotal = lineItems.Sum(i => i.LineTotal.Amount);
        if (subtotal <= 0)
            subtotal = paymentAmount / 1.19m;

        return new MoneyV1 { Amount = Math.Round(subtotal, 2), Currency = currency };
    }

    private static string BuildDocumentNumber(string prefix, Guid paymentId) =>
        $"{prefix}-{DateTime.UtcNow:yyyy}-{paymentId.ToString()[..8].ToUpperInvariant()}";
}
