using Application.Features.Payments.CreatePayment;
using Domain.Enums;
using MediatR;
using Shared.Security.Authorization;

namespace Payments.Endpoints;

public static class CreatePaymentEndpoint
{
    public static IEndpointRouteBuilder MapCreatePayment(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", async (
            CreatePaymentRequest request,
            HttpContext httpContext,
            ICurrentUser user,
            IMediator mediator,
            CancellationToken ct) =>
        {
            // The payer is taken from the verified token, never trusted from the body (FR-005).
            if (!user.IsAdmin && request.UserId != user.Id)
            {
                return Results.Json(
                    new { error = "You can only create payments for your own account.", code = "forbidden", status = 403 },
                    statusCode: StatusCodes.Status403Forbidden);
            }

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
        }).RequireAuthorization();

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
