using Application.Common;
using FluentAssertions;

namespace UnitTests.Application;

public class IdempotencyKeyBuilderTests
{
    [Fact]
    public void ForInvoice_Formats_Order_And_Payment()
    {
        var orderId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var paymentId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        var key = IdempotencyKeyBuilder.ForInvoice(orderId, paymentId);

        key.Should().Be($"invoice:{orderId}:{paymentId}");
    }

    [Fact]
    public void ForReceipt_Formats_Order_And_Payment()
    {
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        IdempotencyKeyBuilder.ForReceipt(orderId, paymentId)
            .Should().Be($"receipt:{orderId}:{paymentId}");
    }

    [Fact]
    public void ForCreditNote_Formats_Payment_And_Amounts_With_Two_Decimals()
    {
        var paymentId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        IdempotencyKeyBuilder.ForCreditNote(paymentId, 10m, 5m)
            .Should().Be($"refund:{paymentId}:10.00:5.00");
    }
}
