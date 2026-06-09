using Application.Features.Payments.CreatePayment;
using Domain.Enums;
using MediatR;

namespace Payments.Endpoints;

public static class CreatePaymentEndpoint
{
    public static IEndpointRouteBuilder MapCreatePayment(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", async (
            CreatePaymentRequest request,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new CreatePaymentCommand(
                request.OrderId,
                request.UserId,
                request.Amount,
                request.Currency,
                request.Method,
                request.IdempotencyKey,
                request.PaymentToken,
                RecipientEmailResolver.Resolve(httpContext, request.RecipientEmail));

            var paymentId = await mediator.Send(command, ct);

            return Results.Created($"/payments/{paymentId}", new { paymentId });
        });

        return app;
    }
}

public record CreatePaymentRequest(
    Guid OrderId,
    Guid UserId,
    decimal Amount,
    string Currency,
    PaymentMethod Method,
    string IdempotencyKey,
    string PaymentToken,
    string? RecipientEmail = null
);
