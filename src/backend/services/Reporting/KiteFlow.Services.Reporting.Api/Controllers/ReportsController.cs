using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KiteFlow.Services.Reporting.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KiteFlow.Services.Reporting.Api.Controllers;

[ApiController]
[Authorize(Policy = "DashboardAccess")]
[Route("api/v1/reports")]
public sealed class ReportsController : ControllerBase
{
    private static readonly CultureInfo PtBrCulture = new("pt-BR");

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ReportingSnapshotService _snapshotService;

    public ReportsController(IHttpClientFactory httpClientFactory, ReportingSnapshotService snapshotService)
    {
        _httpClientFactory = httpClientFactory;
        _snapshotService = snapshotService;
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
                var alerts = BuildAlerts(academics, equipment, finance, maintenanceAlerts);

                return new
                {
                    generatedAtUtc = DateTime.UtcNow,
                    fromUtc,
                    toUtc,
                    academics,
                    equipment,
                    maintenanceAlerts,
                    finance,
                    alerts,
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
                var maintenanceAlerts = await GetJsonAsync("equipment", "/api/v1/maintenance/alerts", "maintenance", serviceErrors, ct);
                var alerts = BuildAlerts(academics, equipment, null, maintenanceAlerts);

                return new
                {
                    generatedAtUtc = DateTime.UtcNow,
                    fromUtc,
                    toUtc,
                    academics,
                    equipment,
                    maintenanceAlerts,
                    alerts,
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
                var alerts = BuildAlerts(null, null, finance, null);
                return new
                {
                    generatedAtUtc = DateTime.UtcNow,
                    fromUtc,
                    toUtc,
                    finance,
                    alerts,
                    serviceErrors
                };
            },
            cancellationToken));
    }

    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        return Ok(await GetOrCreateReportAsync(
            reportName: "alerts",
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
                var alerts = BuildAlerts(academics, equipment, finance, maintenanceAlerts);

                return new
                {
                    generatedAtUtc = DateTime.UtcNow,
                    fromUtc,
                    toUtc,
                    alerts,
                    serviceErrors
                };
            },
            cancellationToken));
    }

    private async Task<JsonElement> GetOrCreateReportAsync(
        string reportName,
        DateTime? fromUtc,
        DateTime? toUtc,
        Func<CancellationToken, Task<object>> factory,
        CancellationToken cancellationToken)
    {
        var schoolIdClaim = User.FindFirst("school_id")?.Value;
        if (!Guid.TryParse(schoolIdClaim, out var schoolId))
        {
            return JsonSerializer.SerializeToElement(new
            {
                generatedAtUtc = DateTime.UtcNow,
                fromUtc,
                toUtc,
                serviceErrors = new[]
                {
                    new
                    {
                        service = "reporting",
                        statusCode = 400,
                        message = "Não foi possível identificar a escola para montar o relatório."
                    }
                }
            });
        }

        return await _snapshotService.GetOrCreateAsync(schoolId, reportName, fromUtc, toUtc, factory, cancellationToken);
    }

    private async Task<JsonElement?> GetJsonAsync(
        string clientName,
        string path,
        string serviceName,
        List<object> serviceErrors,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(clientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        var authorization = Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization) && AuthenticationHeaderValue.TryParse(authorization, out var header))
        {
            request.Headers.Authorization = header;
        }

        try
        {
            var response = await client.SendAsync(request, cancellationToken);
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

    private static IReadOnlyList<ReportAlertItem> BuildAlerts(
        JsonElement? academics,
        JsonElement? equipment,
        JsonElement? finance,
        JsonElement? maintenanceAlerts)
    {
        var alerts = new List<ReportAlertItem>();

        AddFinanceAlerts(alerts, finance);
        AddOperationalAlerts(alerts, academics, equipment);
        AddMaintenanceAlerts(alerts, maintenanceAlerts);

        return alerts
            .OrderByDescending(x => SeverityRank(x.Severity))
            .ThenBy(x => x.Scope)
            .ThenBy(x => x.Title)
            .Take(10)
            .ToList();
    }

    private static void AddFinanceAlerts(List<ReportAlertItem> alerts, JsonElement? finance)
    {
        if (finance is not { ValueKind: JsonValueKind.Object } financeObject)
        {
            return;
        }

        if (TryGetDecimal(financeObject, "receivablesOverdueAmount", out var overdueReceivables) && overdueReceivables > 0)
        {
            alerts.Add(new ReportAlertItem(
                Id: "finance-overdue-receivables",
                Scope: "finance",
                Severity: "critical",
                Title: "Recebimentos em atraso",
                Message: $"{FormatCurrency(overdueReceivables)} em aberto já ultrapassou o vencimento."));
        }

        if (TryGetDecimal(financeObject, "payablesOverdueAmount", out var overduePayables) && overduePayables > 0)
        {
            alerts.Add(new ReportAlertItem(
                Id: "finance-overdue-payables",
                Scope: "finance",
                Severity: "warning",
                Title: "Pagamentos em atraso",
                Message: $"{FormatCurrency(overduePayables)} ainda não foi quitado no prazo."));
        }

        if (TryGetDecimal(financeObject, "grossMargin", out var grossMargin) && grossMargin < 0)
        {
            alerts.Add(new ReportAlertItem(
                Id: "finance-negative-margin",
                Scope: "finance",
                Severity: "critical",
                Title: "Margem bruta negativa",
                Message: $"A operação fechou o período com margem de {FormatCurrency(grossMargin)}."));
        }

        if (TryGetDecimal(financeObject, "unreconciledReceivableAmount", out var unreconciledReceivables) &&
            TryGetDecimal(financeObject, "unreconciledPayableAmount", out var unreconciledPayables) &&
            unreconciledReceivables + unreconciledPayables > 0)
        {
            alerts.Add(new ReportAlertItem(
                Id: "finance-reconciliation-pending",
                Scope: "finance",
                Severity: "info",
                Title: "Conciliação pendente",
                Message: $"Ainda existem {FormatCurrency(unreconciledReceivables + unreconciledPayables)} aguardando conciliação."));
        }
    }

    private static void AddOperationalAlerts(
        List<ReportAlertItem> alerts,
        JsonElement? academics,
        JsonElement? equipment)
    {
        if (academics is { ValueKind: JsonValueKind.Object } academicsObject)
        {
            if (TryGetDecimal(academicsObject, "completionRate", out var completionRate) &&
                TryGetInt32(academicsObject, "totalLessonsInPeriod", out var totalLessonsInPeriod) &&
                totalLessonsInPeriod >= 5 &&
                completionRate < 70m)
            {
                alerts.Add(new ReportAlertItem(
                    Id: "operations-low-completion",
                    Scope: "operations",
                    Severity: "warning",
                    Title: "Baixa taxa de realização",
                    Message: $"Só {completionRate.ToString("0.#", PtBrCulture)}% das aulas do período foram concluídas."));
            }

            if (academicsObject.TryGetProperty("statusBreakdown", out var statusBreakdown) &&
                statusBreakdown.ValueKind == JsonValueKind.Array)
            {
                var noShowCount = statusBreakdown.EnumerateArray()
                    .Where(item => item.TryGetProperty("status", out var status) && string.Equals(status.GetString(), "NoShow", StringComparison.OrdinalIgnoreCase))
                    .Select(item => item.TryGetProperty("count", out var countElement) && countElement.TryGetInt32(out var countValue) ? countValue : 0)
                    .FirstOrDefault();

                if (noShowCount > 0)
                {
                    alerts.Add(new ReportAlertItem(
                        Id: "operations-no-show",
                        Scope: "operations",
                        Severity: "warning",
                        Title: "No-show no período",
                        Message: $"{noShowCount} aula(s) terminaram com ausência do aluno."));
                }
            }
        }

        if (equipment is not { ValueKind: JsonValueKind.Object } equipmentObject)
        {
            return;
        }

        if (TryGetInt32(equipmentObject, "equipmentInAttention", out var equipmentInAttention) && equipmentInAttention > 0)
        {
            alerts.Add(new ReportAlertItem(
                Id: "operations-equipment-attention",
                Scope: "equipment",
                Severity: "warning",
                Title: "Equipamentos em atenção",
                Message: $"{equipmentInAttention} item(ns) exigem acompanhamento operacional."));
        }

        if (TryGetInt32(equipmentObject, "openCheckouts", out var openCheckouts) && openCheckouts > 0)
        {
            alerts.Add(new ReportAlertItem(
                Id: "operations-open-checkouts",
                Scope: "equipment",
                Severity: "info",
                Title: "Checkouts ainda abertos",
                Message: $"{openCheckouts} checkout(s) ainda aguardam devolução ou conferência."));
        }
    }

    private static void AddMaintenanceAlerts(List<ReportAlertItem> alerts, JsonElement? maintenanceAlerts)
    {
        if (maintenanceAlerts is not { ValueKind: JsonValueKind.Array } alertsArray)
        {
            return;
        }

        foreach (var item in alertsArray.EnumerateArray().Take(6))
        {
            var id = item.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N");
            var name = item.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? "Equipamento" : "Equipamento";
            var alertType = item.TryGetProperty("alertType", out var alertTypeElement) ? alertTypeElement.GetString() ?? "warning" : "warning";
            var type = item.TryGetProperty("type", out var typeElement) ? typeElement.GetString() ?? string.Empty : string.Empty;
            var severity = alertType.Equals("Critical", StringComparison.OrdinalIgnoreCase) ? "critical" :
                alertType.Equals("Warning", StringComparison.OrdinalIgnoreCase) ? "warning" :
                "info";

            var messageBuilder = new StringBuilder();
            if (item.TryGetProperty("remainingMinutes", out var remainingMinutesElement) && remainingMinutesElement.TryGetInt32(out var remainingMinutes))
            {
                messageBuilder.Append($"Serviço previsto em {remainingMinutes} minuto(s) de uso restante.");
            }

            if (item.TryGetProperty("remainingDays", out var remainingDaysElement) && remainingDaysElement.TryGetInt32(out var remainingDays))
            {
                if (messageBuilder.Length > 0)
                {
                    messageBuilder.Append(' ');
                }

                messageBuilder.Append($"Faltam {remainingDays} dia(s) para a próxima janela preventiva.");
            }

            if (messageBuilder.Length == 0)
            {
                messageBuilder.Append("Existe uma janela de manutenção próxima para este equipamento.");
            }

            alerts.Add(new ReportAlertItem(
                Id: $"maintenance-{id}",
                Scope: "maintenance",
                Severity: severity,
                Title: string.IsNullOrWhiteSpace(type) ? name : $"{name} · {type}",
                Message: messageBuilder.ToString()));
        }
    }

    private static int SeverityRank(string severity) =>
        severity.Equals("critical", StringComparison.OrdinalIgnoreCase) ? 3 :
        severity.Equals("warning", StringComparison.OrdinalIgnoreCase) ? 2 :
        1;

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

    private static bool TryGetDecimal(JsonElement element, string propertyName, out decimal value)
    {
        value = 0m;
        return element.TryGetProperty(propertyName, out var property) && property.TryGetDecimal(out value);
    }

    private static bool TryGetInt32(JsonElement element, string propertyName, out int value)
    {
        value = 0;
        return element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out value);
    }

    private static string FormatCurrency(decimal amount) => amount.ToString("C", PtBrCulture);

    private sealed record ReportAlertItem(
        string Id,
        string Scope,
        string Severity,
        string Title,
        string Message);
}
