using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Clean_Connect.Infrastructure.Operational;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        var supplied = context.Request.Headers[HeaderName].ToString();
        var correlationId = IsSafe(supplied) ? supplied : Guid.NewGuid().ToString("N");
        context.TraceIdentifier = correlationId;
        context.Items[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
            await next(context);
    }

    public static bool IsSafe(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 64
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}

public static class BackgroundCorrelationScope
{
    public static IDisposable Begin<T>(ILogger<T> logger, string operation) =>
        logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = Guid.NewGuid().ToString("N"),
            ["BackgroundOperation"] = operation
        })!;
}
