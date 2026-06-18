using System.Diagnostics;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Common;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var correlationId = ExtractCorrelationId(request);

        _logger.LogInformation(
            "Handling {RequestName} | CorrelationId: {CorrelationId} | Payload: {Payload}",
            requestName,
            correlationId,
            JsonSerializer.Serialize(request));

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next();
            stopwatch.Stop();

            _logger.LogInformation(
                "Handled {RequestName} in {ElapsedMs}ms | CorrelationId: {CorrelationId}",
                requestName,
                stopwatch.ElapsedMilliseconds,
                correlationId);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "Error handling {RequestName} after {ElapsedMs}ms | CorrelationId: {CorrelationId}",
                requestName,
                stopwatch.ElapsedMilliseconds,
                correlationId);

            throw;
        }
    }

    private static string? ExtractCorrelationId(TRequest request)
    {
        var property = typeof(TRequest).GetProperty("CorrelationId");
        return property?.GetValue(request) as string;
    }
}
