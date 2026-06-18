using Application.Contracts;
using FluentValidation;

namespace Application.Features.Documents.GenerateReceipt;

public class ReceiptDocumentRequestValidator : AbstractValidator<ReceiptDocumentRequestV1>
{
    public ReceiptDocumentRequestValidator()
    {
        RuleFor(x => x.ContractVersion).Equal("v1");
        RuleFor(x => x.TemplateVersion).Equal("receipt-v1");
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.Seller).NotNull();
        RuleFor(x => x.Customer).NotNull();
        RuleFor(x => x.Customer.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.AmountPaid).NotNull();
        RuleFor(x => x.AmountPaid.Currency).NotEmpty();
    }
}
