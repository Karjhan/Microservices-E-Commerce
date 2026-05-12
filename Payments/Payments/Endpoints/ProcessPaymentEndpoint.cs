using Application.Features.Payments.ProcessPayment;
using MediatR;

namespace Payments.Endpoints;

public static class ProcessPaymentEndpoint
{
    public static IEndpointRouteBuilder MapProcessPayment(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{id:guid}/process", async (
            Guid id,
            IMediator mediator,
            CancellationToken ct) =>
        {
            await mediator.Send(new ProcessPaymentCommand(id), ct);
            return Results.Ok(new { message = "Payment processed" });
        });

        return app;
    }
}
