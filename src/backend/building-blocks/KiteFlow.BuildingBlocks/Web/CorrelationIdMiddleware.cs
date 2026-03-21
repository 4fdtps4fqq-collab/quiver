using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace KiteFlow.BuildingBlocks.Web;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(
        RequestDelegate next,
        ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.TraceIdentifier = correlationId;
        context.Items[HeaderName] = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            await _next(context);
        }
    }

    public static string ResolveCorrelationId(HttpContext? context)
    {
        if (context is null)
        {
            return Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        }

        if (context.Items.TryGetValue(HeaderName, out var existingItem) &&
            existingItem is string existingCorrelationId &&
            !string.IsNullOrWhiteSpace(existingCorrelationId))
        {
            return existingCorrelationId;
        }

        if (context.Request.Headers.TryGetValue(HeaderName, out var headerValue))
        {
            var value = headerValue.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        if (!string.IsNullOrWhiteSpace(context.TraceIdentifier))
        {
            return context.TraceIdentifier;
        }

        return Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
    }
}
