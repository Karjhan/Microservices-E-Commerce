using System.Net;
using Domain.Exceptions;

namespace Orders.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OrderException ex)
        {
            await Handle(context, ex, ex.Code switch
            {
                "order.not_found" => HttpStatusCode.NotFound,
                "order.invalid_state" => HttpStatusCode.Conflict,
                _ => HttpStatusCode.BadRequest
            });
        }
        catch (KeyNotFoundException ex)
        {
            await Handle(context, ex, HttpStatusCode.NotFound);
        }
        catch (ArgumentException ex)
        {
            await Handle(context, ex, HttpStatusCode.BadRequest);
        }
        catch (Exception ex)
        {
            await Handle(context, ex, HttpStatusCode.InternalServerError);
        }
    }

    private async Task Handle(HttpContext context, Exception ex, HttpStatusCode status)
    {
        _logger.LogError(ex, "Unhandled exception");

        context.Response.StatusCode = (int)status;

        await context.Response.WriteAsJsonAsync(new
        {
            error = ex.Message,
            code = (ex as OrderException)?.Code,
            status = (int)status,
            traceId = context.TraceIdentifier
        });
    }
}
