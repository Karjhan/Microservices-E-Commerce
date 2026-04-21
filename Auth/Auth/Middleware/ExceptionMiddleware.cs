using System.Net;
using System.Text.Json;
using Contracts.Responses;
using Domain.Exceptions;

namespace Auth.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = context.TraceIdentifier;

        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            await HandleKnownException(context, ex, traceId);
        }
        catch (Exception ex)
        {
            await HandleUnknownException(context, ex, traceId);
        }
    }

    private async Task HandleKnownException(HttpContext context, AppException ex, string traceId)
    {
        _logger.LogWarning(ex,
            "Known application exception occurred. TraceId: {TraceId}, Code: {Code}",
            traceId, ex.Code);

        var response = new ErrorResponse
        {
            Message = ex.Message,
            Code = ex.Code,
            TraceId = traceId
        };

        await WriteResponse(context, HttpStatusCode.BadRequest, response);
    }

    private async Task HandleUnknownException(HttpContext context, Exception ex, string traceId)
    {
        _logger.LogError(ex,
            "Unhandled exception occurred. TraceId: {TraceId}",
            traceId);

        var response = new ErrorResponse
        {
            Message = _env.IsDevelopment()
                ? ex.Message
                : "An unexpected error occurred.",
            Code = "server.internal_error",
            TraceId = traceId,
            Details = _env.IsDevelopment()
                ? new Dictionary<string, object?>
                {
                    ["stackTrace"] = ex.StackTrace
                }
                : null
        };

        await WriteResponse(context, HttpStatusCode.InternalServerError, response);
    }

    private static async Task WriteResponse(
        HttpContext context,
        HttpStatusCode statusCode,
        ErrorResponse response)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var json = JsonSerializer.Serialize(response);

        await context.Response.WriteAsync(json);
    }
}