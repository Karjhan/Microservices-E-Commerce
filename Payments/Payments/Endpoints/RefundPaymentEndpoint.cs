using Application.Features.Payments.RefundPayment;
using MediatR;

namespace Payments.Endpoints;

public static class RefundPaymentEndpoint
{
    public static IEndpointRouteBuilder MapRefundPayment(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{id:guid}/refund", async (
            Guid id,
            RefundPaymentRequest? request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            await mediator.Send(new RefundPaymentCommand(id, request?.Amount), ct);
            return Results.Ok(new { message = "Payment refunded" });
        });

        return app;
    }
}

public record RefundPaymentRequest(decimal? Amount);
