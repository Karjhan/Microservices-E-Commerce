using Application.Features.Payments.RefundPayment;
using MediatR;
using Shared.Security.Authorization;

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
        }).RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }
}

public record RefundPaymentRequest(decimal? Amount);
