namespace Application.Contracts;

public class InvoiceDocumentRequestV1
{
    public string ContractVersion { get; init; } = "v1";
    public string TemplateVersion { get; init; } = "invoice-v1";
    public string DocumentNumber { get; init; } = default!;
    public DateTime IssueDate { get; init; }
    public Guid OrderId { get; init; }
    public Guid PaymentId { get; init; }
    public string ProviderPaymentId { get; init; } = default!;
    public SellerV1 Seller { get; init; } = default!;
    public PartyV1 Customer { get; init; } = default!;
    public List<LineItemV1> LineItems { get; init; } = [];
    public MoneyV1 Subtotal { get; init; } = default!;
    public MoneyV1 Tax { get; init; } = default!;
    public decimal TaxRatePercent { get; init; }
    public MoneyV1 Total { get; init; } = default!;
    public string PaymentMethodSummary { get; init; } = "Card";
}
