using Application.Features.Orders.UpdateShippingAddress;
using MediatR;

namespace Orders.Endpoints;

public static class UpdateShippingAddressEndpoint
{
    public static IEndpointRouteBuilder MapUpdateShippingAddress(this IEndpointRouteBuilder app)
    {
        app.MapPut("/{orderId:guid}/shipping-address", async (
            Guid orderId,
            UpdateShippingAddressRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            await mediator.Send(new UpdateShippingAddressCommand(
                orderId,
                request.Street,
                request.City,
                request.State,
                request.Country,
                request.ZipCode,
                request.FullName,
                request.Phone), ct);

            return Results.NoContent();
        }).RequireAuthorization();

        return app;
    }
}

public record UpdateShippingAddressRequest(
    string Street,
    string City,
    string State,
    string Country,
    string ZipCode,
    string? FullName,
    string? Phone);
