using System.Net;
using Domain.Exceptions;

namespace Catalog.Middleware;

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
        catch (ValidationFailedException ex)
        {
            await WriteProblem(context, ex, HttpStatusCode.BadRequest, ex.Errors);
        }
        catch (ConflictException ex)
        {
            await WriteProblem(context, ex, HttpStatusCode.Conflict);
        }
        catch (BusinessRuleException ex)
        {
            await WriteProblem(context, ex, HttpStatusCode.UnprocessableEntity);
        }
        catch (PayloadTooLargeException ex)
        {
            await WriteProblem(context, ex, HttpStatusCode.RequestEntityTooLarge);
        }
        catch (UnsupportedFileTypeException ex)
        {
            await WriteProblem(context, ex, HttpStatusCode.UnsupportedMediaType);
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            await WriteProblem(
                context,
                new PayloadTooLargeException("The upload is larger than the allowed limit."),
                HttpStatusCode.RequestEntityTooLarge);
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
            status = (int)status
        });
    }

    private async Task WriteProblem(
        HttpContext context,
        DomainException ex,
        HttpStatusCode status,
        IReadOnlyDictionary<string, string[]>? errors = null)
    {
        _logger.LogInformation("Request refused: {Code} ({Status}) {Message}", ex.Code, (int)status, ex.Message);

        if (context.Response.HasStarted)
            throw ex;

        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(new
        {
            type = $"https://httpstatuses.example/{(int)status}",
            title = ReasonPhrase(status),
            status = (int)status,
            code = ex.Code,
            detail = ex.Message,
            error = ex.Message,
            errors
        }, options: null, contentType: "application/problem+json");
    }

    private static string ReasonPhrase(HttpStatusCode status) => status switch
    {
        HttpStatusCode.BadRequest => "Bad Request",
        HttpStatusCode.Conflict => "Conflict",
        HttpStatusCode.UnprocessableEntity => "Unprocessable Entity",
        HttpStatusCode.RequestEntityTooLarge => "Payload Too Large",
        HttpStatusCode.UnsupportedMediaType => "Unsupported Media Type",
        _ => status.ToString()
    };
}
