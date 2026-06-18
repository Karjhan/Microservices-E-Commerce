namespace Application.Common;

public static class IdempotencyKeyBuilder
{
    public static string ForInvoice(Guid orderId, Guid paymentId) =>
        $"invoice:{orderId}:{paymentId}";

    public static string ForReceipt(Guid orderId, Guid paymentId) =>
        $"receipt:{orderId}:{paymentId}";

    public static string ForCreditNote(Guid paymentId, decimal totalRefunded, decimal refundAmount) =>
        $"refund:{paymentId}:{totalRefunded:F2}:{refundAmount:F2}";
}
