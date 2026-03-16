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

    public MaintenanceController(EquipmentDbContext dbContext, ICurrentTenant currentTenant)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
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
                x.ServiceEveryMinutes,
                x.ServiceEveryDays,
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

        rule.ServiceEveryMinutes = request.ServiceEveryMinutes;
        rule.ServiceEveryDays = request.ServiceEveryDays;
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
                x.ServiceDateUtc,
                x.UsageMinutesAtService,
                x.Cost,
                x.Description,
                x.PerformedBy
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
            return BadRequest("EquipmentId is invalid for the current school.");
        }

        var description = (request.Description ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(description))
        {
            return BadRequest("A descrição da manutenção é obrigatória.");
        }

        var record = new MaintenanceRecord
        {
            SchoolId = schoolId,
            EquipmentId = equipment.Id,
            ServiceDateUtc = request.ServiceDateUtc,
            UsageMinutesAtService = equipment.TotalUsageMinutes,
            Cost = request.Cost,
            Description = description,
            PerformedBy = NormalizeNullable(request.PerformedBy)
        };

        _dbContext.MaintenanceRecords.Add(record);
        equipment.LastServiceDateUtc = request.ServiceDateUtc;
        equipment.LastServiceUsageMinutes = equipment.TotalUsageMinutes;
        equipment.CurrentCondition = request.ConditionAfterService;

        await _dbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(GetRecords), new { id = record.Id }, new { recordId = record.Id });
    }

    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts([FromQuery] int nearMinutes = 300, [FromQuery] int nearDays = 15)
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

                    if (remaining <= nearMinutes)
                    {
                        alerts.Add(new
                        {
                            item.Id,
                            item.Name,
                            type = item.Type.ToString(),
                            alertType = "Usage",
                            remainingMinutes = remaining
                        });
                    }
                }

                if (rule.ServiceEveryDays.HasValue)
                {
                    var lastDate = item.LastServiceDateUtc ?? item.CreatedAtUtc;
                    var dueDate = lastDate.AddDays(rule.ServiceEveryDays.Value);
                    var remainingDays = (int)Math.Floor((dueDate - now).TotalDays);
                    if (remainingDays <= nearDays)
                    {
                        alerts.Add(new
                        {
                            item.Id,
                            item.Name,
                            type = item.Type.ToString(),
                            alertType = "Date",
                            dueDateUtc = dueDate,
                            remainingDays
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
                    condition = item.CurrentCondition.ToString()
                });
            }
        }

        return Ok(alerts);
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public sealed record UpsertRuleRequest(
        EquipmentType EquipmentType,
        int? ServiceEveryMinutes,
        int? ServiceEveryDays,
        bool IsActive);

    public sealed record CreateMaintenanceRecordRequest(
        Guid EquipmentId,
        DateTime ServiceDateUtc,
        string Description,
        decimal? Cost,
        string? PerformedBy,
        EquipmentCondition ConditionAfterService);
}
