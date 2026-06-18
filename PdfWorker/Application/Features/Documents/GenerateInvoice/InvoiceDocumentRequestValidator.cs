using Application.Contracts;
using FluentValidation;

namespace Application.Features.Documents.GenerateInvoice;

public class InvoiceDocumentRequestValidator : AbstractValidator<InvoiceDocumentRequestV1>
{
    public InvoiceDocumentRequestValidator()
    {
        RuleFor(x => x.ContractVersion).Equal("v1");
        RuleFor(x => x.TemplateVersion).Equal("invoice-v1");
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.Seller).NotNull();
        RuleFor(x => x.Seller.LegalName).NotEmpty();
        RuleFor(x => x.Customer).NotNull();
        RuleFor(x => x.Customer.Name).NotEmpty();
        RuleFor(x => x.Customer.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Customer.AddressLine1).NotEmpty();
        RuleFor(x => x.LineItems).NotEmpty();
        RuleFor(x => x.Total).NotNull();
        RuleFor(x => x.Total.Currency).NotEmpty();
        RuleForEach(x => x.LineItems).ChildRules(item =>
        {
            item.RuleFor(i => i.LineTotal.Currency).Equal(i => i.UnitPrice.Currency);
        });
    }

    public void ValidateTotalAmount(InvoiceDocumentRequestV1 request, decimal expectedAmount)
    {
        if (Math.Abs(request.Total.Amount - expectedAmount) > 0.01m)
            throw new ValidationException($"Total amount {request.Total.Amount} does not match payment amount {expectedAmount} within tolerance");
    }
}
