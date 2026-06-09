namespace Payments;

public static class RecipientEmailResolver
{
    public const string HeaderName = "X-User-Email";
    public static string? Resolve(HttpContext httpContext, string? fromRequest)
    {
        if (!string.IsNullOrWhiteSpace(fromRequest))
            return fromRequest.Trim();

        if (httpContext.Request.Headers.TryGetValue(HeaderName, out var header)
            && !string.IsNullOrWhiteSpace(header))
            return header.ToString().Trim();

        return null;
    }
}