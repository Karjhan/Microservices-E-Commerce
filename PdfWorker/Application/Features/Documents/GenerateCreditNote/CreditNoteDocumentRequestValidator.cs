using Application.Contracts;
using FluentValidation;

namespace Application.Features.Documents.GenerateCreditNote;

public class CreditNoteDocumentRequestValidator : AbstractValidator<CreditNoteDocumentRequestV1>
{
    public CreditNoteDocumentRequestValidator()
    {
        RuleFor(x => x.ContractVersion).Equal("v1");
        RuleFor(x => x.TemplateVersion).Equal("credit-note-v1");
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.RefundAmount).NotNull();
        RuleFor(x => x.CumulativeRefunded).NotNull();
        RuleFor(x => x.RemainingBalance).NotNull();
        RuleFor(x => x.Customer).NotNull();
        RuleFor(x => x.Customer.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.LineItems)
            .NotEmpty()
            .When(x => !x.IsFullRefund);
    }
}
