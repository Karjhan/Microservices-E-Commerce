using Application.Abstractions.Persistence;
using Application.Features.Payments.GetPayments;
using Domain.Enums;
using MediatR;

namespace Payments.Endpoints;

public static class GetPaymentsEndpoint
{
    public static IEndpointRouteBuilder MapGetPayments(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", async (
            Guid? userId,
            Guid? orderId,
            PaymentStatus? status,
            PaymentMethod? method,
            DateTime? fromDate,
            DateTime? toDate,
            int page,
            int pageSize,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var filter = new PaymentFilter
            {
                UserId = userId,
                OrderId = orderId,
                Status = status,
                Method = method,
                FromDate = fromDate,
                ToDate = toDate,
                Page = page == 0 ? 1 : page,
                PageSize = pageSize == 0 ? 20 : pageSize
            };

            var result = await mediator.Send(new GetPaymentsQuery(filter), ct);
            return Results.Ok(result);
        });

        return app;
    }
}
