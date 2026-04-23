namespace ApiGateway.Middleware;

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string Header = "X-Correlation-Id";

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext context)
    {
        var id = context.Request.Headers[Header].FirstOrDefault()
                 ?? Guid.NewGuid().ToString();

        context.Items[Header] = id;
        context.Response.Headers[Header] = id;

        await _next(context);
    }
}