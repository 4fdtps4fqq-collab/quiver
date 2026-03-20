using System.Net.Http.Json;
using KiteFlow.BuildingBlocks.MultiTenancy;
using KiteFlow.Services.Equipment.Api.Data;
using KiteFlow.Services.Equipment.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Equipment.Api.Controllers;

[ApiController]
[Authorize(Policy = "MaintenanceAccess")]
[Route("api/v1/maintenance")]
public sealed class MaintenanceController : ControllerBase
{
    private readonly EquipmentDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public MaintenanceController(
        EquipmentDbContext dbContext,
        ICurrentTenant currentTenant,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [HttpGet("rules")]
    public async Task<IActionResult> GetRules()
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var items = await _dbContext.MaintenanceRules
            .Where(x => x.SchoolId == schoolId)
            .OrderBy(x => x.EquipmentType)
            .Select(x => new
            {
                x.Id,
                equipmentType = x.EquipmentType.ToString(),
                x.PlanName,
                serviceCategory = x.ServiceCategory.ToString(),
                x.ServiceEveryMinutes,
                x.ServiceEveryDays,
                x.WarningLeadMinutes,
                x.CriticalLeadMinutes,
                x.WarningLeadDays,
                x.CriticalLeadDays,
                x.Checklist,
                x.Notes,
                x.IsActive
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost("rules")]
    public async Task<IActionResult> UpsertRule([FromBody] UpsertRuleRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        if (!request.ServiceEveryMinutes.HasValue && !request.ServiceEveryDays.HasValue)
        {
            return BadRequest("Informe pelo menos um critério para a regra de manutenção.");
        }

        var rule = await _dbContext.MaintenanceRules
            .FirstOrDefaultAsync(x => x.SchoolId == schoolId && x.EquipmentType == request.EquipmentType);

        if (rule is null)
        {
            rule = new MaintenanceRule
            {
                SchoolId = schoolId,
                EquipmentType = request.EquipmentType
            };
            _dbContext.MaintenanceRules.Add(rule);
        }

        rule.PlanName = string.IsNullOrWhiteSpace(request.PlanName) ? "Plano preventivo" : request.PlanName.Trim();
        rule.ServiceCategory = request.ServiceCategory;
        rule.ServiceEveryMinutes = request.ServiceEveryMinutes;
        rule.ServiceEveryDays = request.ServiceEveryDays;
        rule.WarningLeadMinutes = request.WarningLeadMinutes;
        rule.CriticalLeadMinutes = request.CriticalLeadMinutes;
        rule.WarningLeadDays = request.WarningLeadDays;
        rule.CriticalLeadDays = request.CriticalLeadDays;
        rule.Checklist = NormalizeNullable(request.Checklist);
        rule.Notes = NormalizeNullable(request.Notes);
        rule.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync();
        return Ok(new { ruleId = rule.Id });
    }

    [HttpGet("records")]
    public async Task<IActionResult> GetRecords([FromQuery] Guid? equipmentId)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var query = _dbContext.MaintenanceRecords
            .Where(x => x.SchoolId == schoolId)
            .Include(x => x.Equipment)
            .AsQueryable();

        if (equipmentId.HasValue)
        {
            query = query.Where(x => x.EquipmentId == equipmentId.Value);
        }

        var items = await query
            .OrderByDescending(x => x.ServiceDateUtc)
            .Select(x => new
            {
                x.Id,
                x.EquipmentId,
                equipmentName = x.Equipment!.Name,
                equipmentType = x.Equipment.Type.ToString(),
                equipmentOwnershipType = x.Equipment.OwnershipType.ToString(),
                x.ServiceDateUtc,
                x.UsageMinutesAtService,
                x.Cost,
                x.Description,
                x.PerformedBy,
                serviceCategory = x.ServiceCategory.ToString(),
                financialEffect = x.FinancialEffect.ToString(),
                x.CounterpartyName
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost("records")]
    public async Task<IActionResult> CreateRecord([FromBody] CreateMaintenanceRecordRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var equipment = await _dbContext.EquipmentItems.FirstOrDefaultAsync(x =>
            x.Id == request.EquipmentId &&
            x.SchoolId == schoolId);

        if (equipment is null)
        {
            return BadRequest("EquipmentId inválido para a escola atual.");
        }

        var description = (request.Description ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(description))
        {
            return BadRequest("A descrição da manutenção é obrigatória.");
        }

        var financialEffect = equipment.OwnershipType switch
        {
            EquipmentOwnershipType.ThirdParty => MaintenanceFinancialEffect.Revenue,
            EquipmentOwnershipType.SchoolOwned => MaintenanceFinancialEffect.Expense,
            _ => MaintenanceFinancialEffect.None
        };

        var record = new MaintenanceRecord
        {
            SchoolId = schoolId,
            EquipmentId = equipment.Id,
            ServiceDateUtc = request.ServiceDateUtc,
            UsageMinutesAtService = equipment.TotalUsageMinutes,
            Cost = request.Cost,
            Description = description,
            PerformedBy = NormalizeNullable(request.PerformedBy),
            ServiceCategory = request.ServiceCategory,
            FinancialEffect = financialEffect,
            CounterpartyName = NormalizeNullable(request.CounterpartyName)
        };

        _dbContext.MaintenanceRecords.Add(record);
        equipment.LastServiceDateUtc = request.ServiceDateUtc;
        equipment.LastServiceUsageMinutes = equipment.TotalUsageMinutes;
        equipment.CurrentCondition = request.ConditionAfterService;

        await _dbContext.SaveChangesAsync();
        await SyncMaintenanceFinancialImpactAsync(record, equipment);

        return CreatedAtAction(nameof(GetRecords), new { id = record.Id }, new { recordId = record.Id });
    }

    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts()
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var equipment = await _dbContext.EquipmentItems
            .Where(x => x.SchoolId == schoolId && x.IsActive)
            .ToListAsync();

        var rules = await _dbContext.MaintenanceRules
            .Where(x => x.SchoolId == schoolId && x.IsActive)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var alerts = new List<object>();

        foreach (var item in equipment)
        {
            var rule = rules.FirstOrDefault(x => x.EquipmentType == item.Type);
            if (rule is not null)
            {
                if (rule.ServiceEveryMinutes.HasValue)
                {
                    var lastServiceUsage = item.LastServiceUsageMinutes ?? 0;
                    var dueAt = lastServiceUsage + rule.ServiceEveryMinutes.Value;
                    var remaining = dueAt - item.TotalUsageMinutes;

                    var severity = ResolveSeverityByMinutes(remaining, rule);
                    if (severity is not null)
                    {
                        alerts.Add(new
                        {
                            item.Id,
                            item.Name,
                            type = item.Type.ToString(),
                            alertType = "Usage",
                            remainingMinutes = remaining,
                            severity,
                            serviceCategory = rule.ServiceCategory.ToString(),
                            recommendedAction = "Planejar manutenção com base no uso acumulado."
                        });
                    }
                }

                if (rule.ServiceEveryDays.HasValue)
                {
                    var lastDate = item.LastServiceDateUtc ?? item.CreatedAtUtc;
                    var dueDate = lastDate.AddDays(rule.ServiceEveryDays.Value);
                    var remainingDays = (int)Math.Floor((dueDate - now).TotalDays);
                    var severity = ResolveSeverityByDays(remainingDays, rule);
                    if (severity is not null)
                    {
                        alerts.Add(new
                        {
                            item.Id,
                            item.Name,
                            type = item.Type.ToString(),
                            alertType = "Date",
                            dueDateUtc = dueDate,
                            remainingDays,
                            severity,
                            serviceCategory = rule.ServiceCategory.ToString(),
                            recommendedAction = "Reservar janela de oficina antes do vencimento do plano."
                        });
                    }
                }
            }

            if (item.CurrentCondition is EquipmentCondition.NeedsRepair or EquipmentCondition.OutOfService)
            {
                alerts.Add(new
                {
                    item.Id,
                    item.Name,
                    type = item.Type.ToString(),
                    alertType = "Condition",
                    condition = item.CurrentCondition.ToString(),
                    severity = item.CurrentCondition == EquipmentCondition.OutOfService ? "Critical" : "Warning",
                    serviceCategory = MaintenanceServiceCategory.Corrective.ToString(),
                    recommendedAction = "Bloquear o equipamento até uma avaliação técnica."
                });
            }
        }

        return Ok(alerts);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var query = _dbContext.MaintenanceRecords
            .Where(x => x.SchoolId == schoolId)
            .Include(x => x.Equipment)
            .AsQueryable();

        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.ServiceDateUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x => x.ServiceDateUtc <= toUtc.Value);
        }

        var items = await query.ToListAsync();

        return Ok(new
        {
            records = items.Count,
            expenseAmount = items
                .Where(x => x.FinancialEffect == MaintenanceFinancialEffect.Expense)
                .Sum(x => x.Cost ?? 0m),
            revenueAmount = items
                .Where(x => x.FinancialEffect == MaintenanceFinancialEffect.Revenue)
                .Sum(x => x.Cost ?? 0m),
            byCategory = items
                .GroupBy(x => x.ServiceCategory)
                .Select(group => new
                {
                    category = group.Key.ToString(),
                    records = group.Count(),
                    amount = group.Sum(x => x.Cost ?? 0m)
                })
                .OrderByDescending(x => x.amount)
                .ToList(),
            byEquipment = items
                .GroupBy(x => new { x.EquipmentId, EquipmentName = x.Equipment!.Name })
                .Select(group => new
                {
                    equipmentId = group.Key.EquipmentId,
                    equipmentName = group.Key.EquipmentName,
                    records = group.Count(),
                    amount = group.Sum(x => x.Cost ?? 0m)
                })
                .OrderByDescending(x => x.amount)
                .ToList()
        });
    }

    private async Task SyncMaintenanceFinancialImpactAsync(MaintenanceRecord record, EquipmentItem equipment)
    {
        var financeClient = _httpClientFactory.CreateClient("finance");
        var sharedKey = _configuration["InternalServiceAuth:SharedKey"];
        if (!string.IsNullOrWhiteSpace(sharedKey))
        {
            financeClient.DefaultRequestHeaders.Remove("X-KiteFlow-Internal-Key");
            financeClient.DefaultRequestHeaders.TryAddWithoutValidation("X-KiteFlow-Internal-Key", sharedKey);
        }

        var amount = record.Cost ?? 0m;
        if (record.FinancialEffect == MaintenanceFinancialEffect.Revenue)
        {
            await financeClient.PostAsJsonAsync("/api/v1/internal/finance/revenues/automation", new
            {
                schoolId = record.SchoolId,
                sourceType = 4,
                sourceId = record.Id,
                category = "Manutenção de terceiros",
                amount,
                recognizedAtUtc = record.ServiceDateUtc,
                description = $"Serviço em equipamento de terceiro: {equipment.Name}",
                isActive = amount > 0
            });

            return;
        }

        if (record.FinancialEffect == MaintenanceFinancialEffect.Expense)
        {
            await financeClient.PostAsJsonAsync("/api/v1/internal/finance/expenses/automation", new
            {
                schoolId = record.SchoolId,
                sourceType = "equipment-maintenance",
                sourceId = record.Id,
                category = 2,
                amount,
                occurredAtUtc = record.ServiceDateUtc,
                description = $"Manutenção do equipamento {equipment.Name}",
                vendor = record.CounterpartyName ?? record.PerformedBy,
                isActive = amount > 0
            });
        }
    }

    private static string? ResolveSeverityByMinutes(int remainingMinutes, MaintenanceRule rule)
    {
        if (remainingMinutes <= (rule.CriticalLeadMinutes ?? 0))
        {
            return "Critical";
        }

        if (remainingMinutes <= (rule.WarningLeadMinutes ?? 300))
        {
            return "Warning";
        }

        return null;
    }

    private static string? ResolveSeverityByDays(int remainingDays, MaintenanceRule rule)
    {
        if (remainingDays <= (rule.CriticalLeadDays ?? 0))
        {
            return "Critical";
        }

        if (remainingDays <= (rule.WarningLeadDays ?? 15))
        {
            return "Warning";
        }

        return null;
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public sealed record UpsertRuleRequest(
        EquipmentType EquipmentType,
        string? PlanName,
        MaintenanceServiceCategory ServiceCategory,
        int? ServiceEveryMinutes,
        int? ServiceEveryDays,
        int? WarningLeadMinutes,
        int? CriticalLeadMinutes,
        int? WarningLeadDays,
        int? CriticalLeadDays,
        string? Checklist,
        string? Notes,
        bool IsActive);

    public sealed record CreateMaintenanceRecordRequest(
        Guid EquipmentId,
        DateTime ServiceDateUtc,
        string Description,
        decimal? Cost,
        string? PerformedBy,
        string? CounterpartyName,
        MaintenanceServiceCategory ServiceCategory,
        EquipmentCondition ConditionAfterService);
}
