using Application.Contracts;
using Application.Features.Documents.GenerateInvoice;
using FluentAssertions;
using FluentValidation;

namespace UnitTests.Application;

public class InvoiceDocumentRequestValidatorTests
{
    private readonly InvoiceDocumentRequestValidator _validator = new();

    [Fact]
    public async Task Should_Pass_For_Valid_Request()
    {
        var request = CreateValidRequest();

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Fail_When_Contract_Version_Wrong()
    {
        var request = CreateValidRequest();
        request = CloneWith(request, contractVersion: "v2");

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Should_Fail_When_Line_Items_Empty()
    {
        var request = CreateValidRequest();
        request = CloneWith(request, lineItems: []);

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateTotalAmount_Should_Throw_When_Outside_Tolerance()
    {
        var request = CreateValidRequest();
        request = CloneWith(request, total: new MoneyV1 { Amount = 100m, Currency = "EUR" });

        var act = () => _validator.ValidateTotalAmount(request, 50m);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void ValidateTotalAmount_Should_Not_Throw_Within_Tolerance()
    {
        var request = CreateValidRequest();
        request = CloneWith(request, total: new MoneyV1 { Amount = 49.995m, Currency = "EUR" });

        var act = () => _validator.ValidateTotalAmount(request, 50m);

        act.Should().NotThrow();
    }

    private static InvoiceDocumentRequestV1 CloneWith(
        InvoiceDocumentRequestV1 source,
        string? contractVersion = null,
        List<LineItemV1>? lineItems = null,
        MoneyV1? total = null) =>
        new()
        {
            ContractVersion = contractVersion ?? source.ContractVersion,
            TemplateVersion = source.TemplateVersion,
            DocumentNumber = source.DocumentNumber,
            IssueDate = source.IssueDate,
            OrderId = source.OrderId,
            PaymentId = source.PaymentId,
            ProviderPaymentId = source.ProviderPaymentId,
            Seller = source.Seller,
            Customer = source.Customer,
            LineItems = lineItems ?? source.LineItems,
            Subtotal = source.Subtotal,
            Tax = source.Tax,
            TaxRatePercent = source.TaxRatePercent,
            Total = total ?? source.Total,
            PaymentMethodSummary = source.PaymentMethodSummary
        };

    private static InvoiceDocumentRequestV1 CreateValidRequest() => new()
    {
        ContractVersion = "v1",
        TemplateVersion = "invoice-v1",
        DocumentNumber = "INV-1",
        IssueDate = DateTime.UtcNow,
        OrderId = Guid.NewGuid(),
        PaymentId = Guid.NewGuid(),
        ProviderPaymentId = "pi_test",
        Seller = new SellerV1
        {
            LegalName = "Seller GmbH",
            VatId = "DE1",
            Email = "s@s.de",
            AddressLine1 = "Str 1",
            City = "Munich",
            PostalCode = "80331",
            Country = "DE"
        },
        Customer = new PartyV1
        {
            Name = "Jane",
            Email = "jane@example.com",
            AddressLine1 = "Main 1",
            City = "Berlin",
            PostalCode = "10115",
            Country = "DE"
        },
        LineItems =
        [
            new LineItemV1
            {
                ProductId = Guid.NewGuid(),
                Description = "Item",
                Quantity = 1,
                UnitPrice = new MoneyV1 { Amount = 50m, Currency = "EUR" },
                LineTotal = new MoneyV1 { Amount = 50m, Currency = "EUR" }
            }
        ],
        Subtotal = new MoneyV1 { Amount = 42.02m, Currency = "EUR" },
        Tax = new MoneyV1 { Amount = 7.98m, Currency = "EUR" },
        TaxRatePercent = 19m,
        Total = new MoneyV1 { Amount = 50m, Currency = "EUR" }
    };
}
