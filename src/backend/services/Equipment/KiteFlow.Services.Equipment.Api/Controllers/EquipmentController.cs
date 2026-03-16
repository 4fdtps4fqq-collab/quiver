using KiteFlow.BuildingBlocks.MultiTenancy;
using KiteFlow.Services.Equipment.Api.Data;
using KiteFlow.Services.Equipment.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Equipment.Api.Controllers;

[ApiController]
[Authorize(Policy = "EquipmentAccess")]
[Route("api/v1/equipment-items")]
public sealed class EquipmentController : ControllerBase
{
    private readonly EquipmentDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;

    public EquipmentController(EquipmentDbContext dbContext, ICurrentTenant currentTenant)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] EquipmentType? type, [FromQuery] EquipmentCondition? condition)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var query = _dbContext.EquipmentItems
            .Where(x => x.SchoolId == schoolId)
            .Include(x => x.Storage)
            .AsQueryable();

        if (type.HasValue)
        {
            query = query.Where(x => x.Type == type.Value);
        }

        if (condition.HasValue)
        {
            query = query.Where(x => x.CurrentCondition == condition.Value);
        }

        var items = await query
            .OrderBy(x => x.Type)
            .ThenBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                type = x.Type.ToString(),
                x.TagCode,
                x.Brand,
                x.Model,
                x.SizeLabel,
                condition = x.CurrentCondition.ToString(),
                x.TotalUsageMinutes,
                x.LastServiceDateUtc,
                x.LastServiceUsageMinutes,
                x.IsActive,
                x.StorageId,
                storageName = x.Storage!.Name
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> GetHistory(Guid id)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var equipment = await _dbContext.EquipmentItems
            .Include(x => x.Storage)
            .FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);

        if (equipment is null)
        {
            return NotFound();
        }

        var usage = await _dbContext.EquipmentUsageLogs
            .Where(x => x.SchoolId == schoolId && x.EquipmentId == id)
            .OrderByDescending(x => x.RecordedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.LessonId,
                x.CheckoutItemId,
                x.UsageMinutes,
                conditionAfter = x.ConditionAfter.ToString(),
                x.RecordedAtUtc
            })
            .ToListAsync();

        var maintenance = await _dbContext.MaintenanceRecords
            .Where(x => x.SchoolId == schoolId && x.EquipmentId == id)
            .OrderByDescending(x => x.ServiceDateUtc)
            .Select(x => new
            {
                x.Id,
                x.ServiceDateUtc,
                x.UsageMinutesAtService,
                x.Cost,
                x.Description,
                x.PerformedBy
            })
            .ToListAsync();

        return Ok(new
        {
            equipment = new
            {
                equipment.Id,
                equipment.Name,
                type = equipment.Type.ToString(),
                equipment.TagCode,
                equipment.Brand,
                equipment.Model,
                equipment.SizeLabel,
                condition = equipment.CurrentCondition.ToString(),
                equipment.TotalUsageMinutes,
                equipment.LastServiceDateUtc,
                equipment.LastServiceUsageMinutes,
                storageName = equipment.Storage!.Name
            },
            usage,
            maintenance
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertEquipmentRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var validation = await ValidateStorageAsync(request.StorageId, schoolId);
        if (validation is IActionResult error)
        {
            return error;
        }

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest("O nome do equipamento é obrigatório.");
        }

        var item = new EquipmentItem
        {
            SchoolId = schoolId,
            StorageId = request.StorageId,
            Name = name,
            Type = request.Type,
            TagCode = NormalizeNullable(request.TagCode),
            Brand = NormalizeNullable(request.Brand),
            Model = NormalizeNullable(request.Model),
            SizeLabel = NormalizeNullable(request.SizeLabel),
            CurrentCondition = request.CurrentCondition,
            IsActive = true
        };

        _dbContext.EquipmentItems.Add(item);
        await _dbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { id = item.Id }, new { equipmentId = item.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEquipmentRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var item = await _dbContext.EquipmentItems.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
        if (item is null)
        {
            return NotFound();
        }

        var validation = await ValidateStorageAsync(request.StorageId, schoolId);
        if (validation is IActionResult error)
        {
            return error;
        }

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest("O nome do equipamento é obrigatório.");
        }

        item.StorageId = request.StorageId;
        item.Name = name;
        item.Type = request.Type;
        item.TagCode = NormalizeNullable(request.TagCode);
        item.Brand = NormalizeNullable(request.Brand);
        item.Model = NormalizeNullable(request.Model);
        item.SizeLabel = NormalizeNullable(request.SizeLabel);
        item.CurrentCondition = request.CurrentCondition;
        item.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync();
        return Ok();
    }

    private async Task<IActionResult?> ValidateStorageAsync(Guid storageId, Guid schoolId)
    {
        var storageExists = await _dbContext.GearStorages.AnyAsync(x =>
            x.Id == storageId &&
            x.SchoolId == schoolId &&
            x.IsActive);

        return storageExists ? null : BadRequest("StorageId is invalid for the current school.");
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public sealed record UpsertEquipmentRequest(
        Guid StorageId,
        string Name,
        EquipmentType Type,
        string? TagCode,
        string? Brand,
        string? Model,
        string? SizeLabel,
        EquipmentCondition CurrentCondition);

    public sealed record UpdateEquipmentRequest(
        Guid StorageId,
        string Name,
        EquipmentType Type,
        string? TagCode,
        string? Brand,
        string? Model,
        string? SizeLabel,
        EquipmentCondition CurrentCondition,
        bool IsActive);
}
