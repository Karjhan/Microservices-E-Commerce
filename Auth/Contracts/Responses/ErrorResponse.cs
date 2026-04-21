namespace Contracts.Responses;

public sealed record ErrorResponse
{
    public string Message { get; init; } = default!;
    public string? Code { get; init; }
    public string TraceId { get; init; } = default!;
    public Dictionary<string, object?>? Details { get; init; }
}