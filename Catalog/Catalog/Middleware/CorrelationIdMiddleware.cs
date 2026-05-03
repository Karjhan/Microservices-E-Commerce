namespace Catalog.Middleware;

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string Header = "X-Correlation-Id";

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        var correlationId = context.Request.Headers[Header].FirstOrDefault()
                            ?? Guid.NewGuid().ToString();

        context.Items[Header] = correlationId;
        context.Response.Headers[Header] = correlationId;

        await _next(context);
    }
}