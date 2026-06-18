namespace Application.Contracts;

public class ReceiptDocumentRequestV1
{
    public string ContractVersion { get; init; } = "v1";
    public string TemplateVersion { get; init; } = "receipt-v1";
    public string ReceiptNumber { get; init; } = default!;
    public DateTime IssueDate { get; init; }
    public Guid OrderId { get; init; }
    public Guid PaymentId { get; init; }
    public string ProviderPaymentId { get; init; } = default!;
    public SellerV1 Seller { get; init; } = default!;
    public PartyV1 Customer { get; init; } = default!;
    public MoneyV1 AmountPaid { get; init; } = default!;
    public string PaymentMethodSummary { get; init; } = "Card";
    public DateTime PaidAt { get; init; }
}
