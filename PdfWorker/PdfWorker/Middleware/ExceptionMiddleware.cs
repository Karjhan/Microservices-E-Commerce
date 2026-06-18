using System.Net;
using Domain.Exceptions;

namespace PdfWorker.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
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
        catch (DocumentException ex)
        {
            var status = ex switch
            {
                DocumentNotFoundException => HttpStatusCode.NotFound,
                DocumentValidationException => HttpStatusCode.BadRequest,
                _ => HttpStatusCode.BadRequest
            };

            await Handle(context, ex, status, (ex as DocumentException)?.Code);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await Handle(context, ex, HttpStatusCode.InternalServerError, "internal.error");
        }
    }

    private static async Task Handle(
        HttpContext context,
        Exception ex,
        HttpStatusCode status,
        string? code)
    {
        context.Response.StatusCode = (int)status;
        await context.Response.WriteAsJsonAsync(new
        {
            title = ex.Message,
            status = (int)status,
            detail = ex.Message,
            code,
            traceId = context.TraceIdentifier
        });
    }
}
