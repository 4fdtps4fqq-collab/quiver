using KiteFlow.BuildingBlocks.MultiTenancy;
using KiteFlow.Services.Equipment.Api.Data;
using KiteFlow.Services.Equipment.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Equipment.Api.Controllers;

[ApiController]
[Authorize(Policy = "EquipmentAccess")]
[Route("api/v1/equipment")]
public sealed class EquipmentOverviewController : ControllerBase
{
    private readonly EquipmentDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;

    public EquipmentOverviewController(EquipmentDbContext dbContext, ICurrentTenant currentTenant)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            return BadRequest("A data inicial deve ser menor ou igual a data final.");
        }

        var usageLogsQuery = _dbContext.EquipmentUsageLogs.Where(x => x.SchoolId == schoolId);
        if (fromUtc.HasValue)
        {
            usageLogsQuery = usageLogsQuery.Where(x => x.RecordedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            usageLogsQuery = usageLogsQuery.Where(x => x.RecordedAtUtc <= toUtc.Value);
        }

        var checkoutsQuery = _dbContext.LessonEquipmentCheckouts.Where(x => x.SchoolId == schoolId);
        if (fromUtc.HasValue)
        {
            checkoutsQuery = checkoutsQuery.Where(x => x.CheckedOutAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            checkoutsQuery = checkoutsQuery.Where(x => x.CheckedOutAtUtc <= toUtc.Value);
        }

        var maintenanceQuery = _dbContext.MaintenanceRecords.Where(x => x.SchoolId == schoolId);
        if (fromUtc.HasValue)
        {
            maintenanceQuery = maintenanceQuery.Where(x => x.ServiceDateUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            maintenanceQuery = maintenanceQuery.Where(x => x.ServiceDateUtc <= toUtc.Value);
        }

        var usageSeries = await usageLogsQuery
            .GroupBy(x => x.RecordedAtUtc.Date)
            .Select(g => new
            {
                day = g.Key,
                usageMinutes = g.Sum(x => x.UsageMinutes)
            })
            .ToListAsync();

        var checkoutSeries = await checkoutsQuery
            .GroupBy(x => x.CheckedOutAtUtc.Date)
            .Select(g => new
            {
                day = g.Key,
                checkouts = g.Count()
            })
            .ToListAsync();

        var maintenanceSeries = await maintenanceQuery
            .GroupBy(x => x.ServiceDateUtc.Date)
            .Select(g => new
            {
                day = g.Key,
                maintenanceRecords = g.Count()
            })
            .ToListAsync();

        var activitySeries = usageSeries
            .Select(x => x.day)
            .Union(checkoutSeries.Select(x => x.day))
            .Union(maintenanceSeries.Select(x => x.day))
            .Distinct()
            .OrderBy(x => x)
            .Select(day => new
            {
                bucketStartUtc = day,
                bucketLabel = day.ToString("dd/MM"),
                usageMinutes = usageSeries.FirstOrDefault(x => x.day == day)?.usageMinutes ?? 0,
                checkouts = checkoutSeries.FirstOrDefault(x => x.day == day)?.checkouts ?? 0,
                maintenanceRecords = maintenanceSeries.FirstOrDefault(x => x.day == day)?.maintenanceRecords ?? 0
            })
            .ToList();

        var conditionBreakdown = await _dbContext.EquipmentItems
            .Where(x => x.SchoolId == schoolId && x.IsActive)
            .GroupBy(x => x.CurrentCondition)
            .Select(g => new
            {
                condition = g.Key.ToString(),
                count = g.Count()
            })
            .OrderByDescending(x => x.count)
            .ToListAsync();

        return Ok(new
        {
            fromUtc,
            toUtc,
            storages = await _dbContext.GearStorages.CountAsync(x => x.SchoolId == schoolId && x.IsActive),
            equipment = await _dbContext.EquipmentItems.CountAsync(x => x.SchoolId == schoolId && x.IsActive),
            equipmentInAttention = await _dbContext.EquipmentItems.CountAsync(x =>
                x.SchoolId == schoolId &&
                (x.CurrentCondition == EquipmentCondition.Attention ||
                 x.CurrentCondition == EquipmentCondition.NeedsRepair ||
                 x.CurrentCondition == EquipmentCondition.OutOfService)),
            openCheckouts = await _dbContext.LessonEquipmentCheckouts.CountAsync(x =>
                x.SchoolId == schoolId && x.CheckedInAtUtc == null),
            pendingMaintenance = await _dbContext.MaintenanceRules.CountAsync(x =>
                x.SchoolId == schoolId && x.IsActive),
            usageMinutesInPeriod = await usageLogsQuery.SumAsync(x => (int?)x.UsageMinutes) ?? 0,
            checkoutsInPeriod = await checkoutsQuery.CountAsync(),
            maintenanceExecutedInPeriod = await maintenanceQuery.CountAsync(),
            conditionBreakdown,
            activitySeries
        });
    }
}
