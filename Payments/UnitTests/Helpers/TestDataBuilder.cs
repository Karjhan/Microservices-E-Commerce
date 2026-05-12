using Domain.Entities;
using Domain.Enums;

namespace UnitTests.Helpers;

public static class TestDataBuilder
{
    public static Payment CreatePayment(
        PaymentMethod method = PaymentMethod.CreditCard,
        string? paymentToken = "pm_test_token")
    {
        return new Payment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "EUR",
            method,
            Guid.NewGuid().ToString(),
            paymentToken);
    }

    public static Payment CreateCompletedPayment()
    {
        var payment = CreatePayment();
        payment.MarkProcessing();
        payment.MarkCompleted("pi_test_provider_id");
        return payment;
    }
}
