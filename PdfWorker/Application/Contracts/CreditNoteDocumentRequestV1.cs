namespace Application.Contracts;

public class CreditNoteDocumentRequestV1
{
    public string ContractVersion { get; init; } = "v1";
    public string TemplateVersion { get; init; } = "credit-note-v1";
    public string CreditNoteNumber { get; init; } = default!;
    public DateTime IssueDate { get; init; }
    public Guid OrderId { get; init; }
    public Guid PaymentId { get; init; }
    public MoneyV1 OriginalPaymentAmount { get; init; } = default!;
    public MoneyV1 RefundAmount { get; init; } = default!;
    public MoneyV1 CumulativeRefunded { get; init; } = default!;
    public MoneyV1 RemainingBalance { get; init; } = default!;
    public bool IsFullRefund { get; init; }
    public SellerV1 Seller { get; init; } = default!;
    public PartyV1 Customer { get; init; } = default!;
    public List<LineItemV1> LineItems { get; init; } = [];
    public string? Reason { get; init; }
}
