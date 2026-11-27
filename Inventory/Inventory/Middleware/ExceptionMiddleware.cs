using System.Net;
using Domain.Exceptions;

namespace Inventory.Middleware;

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
        catch (InventoryException ex)
        {
            await Handle(context, ex, ex switch
            {
                InventoryValidationException => HttpStatusCode.BadRequest,
                InvalidAdjustmentException => HttpStatusCode.UnprocessableEntity,
                ConcurrencyConflictException => HttpStatusCode.Conflict,
                _ => ex.Code switch
                {
                    "inventory.not_found" => HttpStatusCode.NotFound,
                    "inventory.insufficient_stock" => HttpStatusCode.Conflict,
                    "inventory.invalid_reservation_state" => HttpStatusCode.Conflict,
                    _ => HttpStatusCode.BadRequest
                }
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
        if (status == HttpStatusCode.InternalServerError)
            _logger.LogError(ex, "Unhandled exception");
        else
            _logger.LogInformation("Request refused: {Code} ({Status}) {Message}",
                (ex as InventoryException)?.Code, (int)status, ex.Message);

        if (context.Response.HasStarted)
            throw ex;

        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(new
        {
            type = $"https://httpstatuses.example/{(int)status}",
            title = ReasonPhrase(status),
            status = (int)status,
            code = (ex as InventoryException)?.Code,
            detail = ex.Message,
            error = ex.Message,
            errors = (ex as InventoryException)?.Errors,
            traceId = context.TraceIdentifier
        }, options: null, contentType: "application/problem+json");
    }

    private static string ReasonPhrase(HttpStatusCode status) => status switch
    {
        HttpStatusCode.BadRequest => "Bad Request",
        HttpStatusCode.NotFound => "Not Found",
        HttpStatusCode.Conflict => "Conflict",
        HttpStatusCode.UnprocessableEntity => "Unprocessable Entity",
        HttpStatusCode.InternalServerError => "Internal Server Error",
        _ => status.ToString()
    };
}
