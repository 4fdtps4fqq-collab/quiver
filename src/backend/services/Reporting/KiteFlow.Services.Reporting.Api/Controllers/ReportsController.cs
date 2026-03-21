using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace KiteFlow.Services.Reporting.Api.Controllers;

[ApiController]
[Authorize(Policy = "DashboardAccess")]
[Route("api/v1/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _memoryCache;

    public ReportsController(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache)
    {
        _httpClientFactory = httpClientFactory;
        _memoryCache = memoryCache;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        return Ok(await GetOrCreateReportAsync(
            reportName: "dashboard",
            fromUtc,
            toUtc,
            async ct =>
            {
                var queryString = BuildQueryString(fromUtc, toUtc);
                var serviceErrors = new List<object>();
                var academics = await GetJsonAsync("academics", $"/api/v1/academics/overview{queryString}", "academics", serviceErrors, ct);
                var equipment = await GetJsonAsync("equipment", $"/api/v1/equipment/overview{queryString}", "equipment", serviceErrors, ct);
                var maintenanceAlerts = await GetJsonAsync("equipment", "/api/v1/maintenance/alerts", "maintenance", serviceErrors, ct);
                var finance = await GetJsonAsync("finance", $"/api/v1/finance/overview{queryString}", "finance", serviceErrors, ct);

                return new
                {
                    generatedAtUtc = DateTime.UtcNow,
                    fromUtc,
                    toUtc,
                    academics,
                    equipment,
                    maintenanceAlerts,
                    finance,
                    serviceErrors
                };
            },
            cancellationToken));
    }

    [HttpGet("operations")]
    public async Task<IActionResult> GetOperations(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        return Ok(await GetOrCreateReportAsync(
            reportName: "operations",
            fromUtc,
            toUtc,
            async ct =>
            {
                var queryString = BuildQueryString(fromUtc, toUtc);
                var serviceErrors = new List<object>();
                var academics = await GetJsonAsync("academics", $"/api/v1/academics/overview{queryString}", "academics", serviceErrors, ct);
                var equipment = await GetJsonAsync("equipment", $"/api/v1/equipment/overview{queryString}", "equipment", serviceErrors, ct);

                return new
                {
                    generatedAtUtc = DateTime.UtcNow,
                    fromUtc,
                    toUtc,
                    academics,
                    equipment,
                    serviceErrors
                };
            },
            cancellationToken));
    }

    [HttpGet("financial")]
    public async Task<IActionResult> GetFinancial(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        return Ok(await GetOrCreateReportAsync(
            reportName: "financial",
            fromUtc,
            toUtc,
            async ct =>
            {
                var queryString = BuildQueryString(fromUtc, toUtc);
                var serviceErrors = new List<object>();
                var finance = await GetJsonAsync("finance", $"/api/v1/finance/overview{queryString}", "finance", serviceErrors, ct);
                return new
                {
                    generatedAtUtc = DateTime.UtcNow,
                    fromUtc,
                    toUtc,
                    finance,
                    serviceErrors
                };
            },
            cancellationToken));
    }

    private async Task<object> GetOrCreateReportAsync(
        string reportName,
        DateTime? fromUtc,
        DateTime? toUtc,
        Func<CancellationToken, Task<object>> factory,
        CancellationToken cancellationToken)
    {
        var schoolId = User.FindFirst("school_id")?.Value ?? "system";
        var cacheKey = $"{reportName}:{schoolId}:{fromUtc:O}:{toUtc:O}";

        if (_memoryCache.TryGetValue(cacheKey, out object? cached) && cached is not null)
        {
            return cached;
        }

        var payload = await factory(cancellationToken);
        _memoryCache.Set(cacheKey, payload, TimeSpan.FromSeconds(15));
        return payload;
    }

    private async Task<JsonElement?> GetJsonAsync(
        string clientName,
        string path,
        string serviceName,
        List<object> serviceErrors,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(clientName);
        var authorization = Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization) && AuthenticationHeaderValue.TryParse(authorization, out var header))
        {
            client.DefaultRequestHeaders.Authorization = header;
        }

        try
        {
            var response = await client.GetAsync(path, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                serviceErrors.Add(new
                {
                    service = serviceName,
                    statusCode = (int)response.StatusCode,
                    message = $"O serviço {serviceName} respondeu com erro."
                });
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return document.RootElement.Clone();
        }
        catch (HttpRequestException)
        {
            serviceErrors.Add(new
            {
                service = serviceName,
                statusCode = 503,
                message = $"O serviço {serviceName} não está disponível no momento."
            });
            return null;
        }
    }

    private static string BuildQueryString(DateTime? fromUtc, DateTime? toUtc)
    {
        var builder = new StringBuilder();

        if (fromUtc.HasValue)
        {
            builder.Append(builder.Length == 0 ? '?' : '&');
            builder.Append("fromUtc=");
            builder.Append(Uri.EscapeDataString(fromUtc.Value.ToString("O")));
        }

        if (toUtc.HasValue)
        {
            builder.Append(builder.Length == 0 ? '?' : '&');
            builder.Append("toUtc=");
            builder.Append(Uri.EscapeDataString(toUtc.Value.ToString("O")));
        }

        return builder.ToString();
    }
}
