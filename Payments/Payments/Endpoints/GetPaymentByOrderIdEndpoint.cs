using Application.Features.Payments.GetPaymentByOrderId;
using MediatR;

namespace Payments.Endpoints;

public static class GetPaymentByOrderIdEndpoint
{
    public static IEndpointRouteBuilder MapGetPaymentByOrderId(this IEndpointRouteBuilder app)
    {
        app.MapGet("/by-order/{orderId:guid}", async (
            Guid orderId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetPaymentByOrderIdQuery(orderId), ct);
            return Results.Ok(result);
        });

        return app;
    }
}
