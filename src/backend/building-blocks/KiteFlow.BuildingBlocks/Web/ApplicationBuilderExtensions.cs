using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace KiteFlow.BuildingBlocks.Web;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseKiteFlowDefaults(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseSwagger();
        app.UseSwaggerUI();
        app.UseAuthentication();
        app.UseMiddleware<MustChangePasswordMiddleware>();
        app.UseAuthorization();
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteHealthResponseAsync
        });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            ResponseWriter = WriteHealthResponseAsync
        });
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = WriteHealthResponseAsync
        });

        return app;
    }

    private static async Task WriteHealthResponseAsync(HttpContext context, Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport report)
    {
        context.Response.ContentType = "application/json";
        await JsonSerializer.SerializeAsync(context.Response.Body, new
        {
            status = report.Status.ToString(),
            duration = report.TotalDuration.TotalMilliseconds,
            correlationId = CorrelationIdMiddleware.ResolveCorrelationId(context),
            entries = report.Entries.ToDictionary(
                static entry => entry.Key,
                static entry => new
                {
                    status = entry.Value.Status.ToString(),
                    duration = entry.Value.Duration.TotalMilliseconds,
                    description = entry.Value.Description
                })
        });
    }
}
