using Application.Features.Payments.GetPaymentById;
using MediatR;

namespace Payments.Endpoints;

public static class GetPaymentByIdEndpoint
{
    public static IEndpointRouteBuilder MapGetPaymentById(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{id:guid}", async (
            Guid id,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetPaymentByIdQuery(id), ct);
            return Results.Ok(result);
        });

        return app;
    }
}
