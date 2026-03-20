using KiteFlow.BuildingBlocks.MultiTenancy;
using KiteFlow.Services.Equipment.Api.Data;
using KiteFlow.Services.Equipment.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Equipment.Api.Controllers;

[ApiController]
[Authorize(Policy = "EquipmentAccess")]
[Route("api/v1/equipment-kits")]
public sealed class EquipmentKitsController : ControllerBase
{
    private readonly EquipmentDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;

    public EquipmentKitsController(EquipmentDbContext dbContext, ICurrentTenant currentTenant)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var kits = await _dbContext.EquipmentKits
            .Where(x => x.SchoolId == schoolId)
            .OrderBy(x => x.Name)
            .ToListAsync();

        var kitIds = kits.Select(x => x.Id).ToList();
        var items = await _dbContext.EquipmentKitItems
            .Where(x => x.SchoolId == schoolId && kitIds.Contains(x.KitId))
            .Join(_dbContext.EquipmentItems.Where(x => x.SchoolId == schoolId),
                item => item.EquipmentId,
                equipment => equipment.Id,
                (item, equipment) => new
                {
                    item.KitId,
                    equipment.Id,
                    equipment.Name,
                    type = equipment.Type.ToString()
                })
            .ToListAsync();

        return Ok(kits.Select(kit => new
        {
            kit.Id,
            kit.Name,
            kit.Description,
            kit.IsActive,
            items = items
                .Where(x => x.KitId == kit.Id)
                .Select(x => new
                {
                    equipmentId = x.Id,
                    x.Name,
                    x.type
                })
                .ToList()
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertEquipmentKitRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest("O nome do kit é obrigatório.");
        }

        var equipmentIds = (request.EquipmentIds ?? []).Where(x => x != Guid.Empty).Distinct().ToList();
        if (equipmentIds.Count == 0)
        {
            return BadRequest("Selecione pelo menos um equipamento para o kit.");
        }

        var validCount = await _dbContext.EquipmentItems.CountAsync(x =>
            x.SchoolId == schoolId &&
            equipmentIds.Contains(x.Id) &&
            x.IsActive);

        if (validCount != equipmentIds.Count)
        {
            return BadRequest("Um ou mais equipamentos do kit são inválidos ou estão inativos.");
        }

        var kit = new EquipmentKit
        {
            SchoolId = schoolId,
            Name = name,
            Description = NormalizeNullable(request.Description),
            IsActive = true
        };

        _dbContext.EquipmentKits.Add(kit);

        foreach (var equipmentId in equipmentIds)
        {
            _dbContext.EquipmentKitItems.Add(new EquipmentKitItem
            {
                SchoolId = schoolId,
                KitId = kit.Id,
                EquipmentId = equipmentId
            });
        }

        await _dbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { id = kit.Id }, new { kitId = kit.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEquipmentKitRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var kit = await _dbContext.EquipmentKits.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
        if (kit is null)
        {
            return NotFound();
        }

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest("O nome do kit é obrigatório.");
        }

        var equipmentIds = (request.EquipmentIds ?? []).Where(x => x != Guid.Empty).Distinct().ToList();
        if (equipmentIds.Count == 0)
        {
            return BadRequest("Selecione pelo menos um equipamento para o kit.");
        }

        var validCount = await _dbContext.EquipmentItems.CountAsync(x =>
            x.SchoolId == schoolId &&
            equipmentIds.Contains(x.Id) &&
            x.IsActive);

        if (validCount != equipmentIds.Count)
        {
            return BadRequest("Um ou mais equipamentos do kit são inválidos ou estão inativos.");
        }

        kit.Name = name;
        kit.Description = NormalizeNullable(request.Description);
        kit.IsActive = request.IsActive;

        var existingItems = await _dbContext.EquipmentKitItems
            .Where(x => x.SchoolId == schoolId && x.KitId == id)
            .ToListAsync();

        _dbContext.EquipmentKitItems.RemoveRange(existingItems);
        foreach (var equipmentId in equipmentIds)
        {
            _dbContext.EquipmentKitItems.Add(new EquipmentKitItem
            {
                SchoolId = schoolId,
                KitId = kit.Id,
                EquipmentId = equipmentId
            });
        }

        await _dbContext.SaveChangesAsync();
        return Ok();
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public sealed record UpsertEquipmentKitRequest(string Name, string? Description, List<Guid>? EquipmentIds);

    public sealed record UpdateEquipmentKitRequest(string Name, string? Description, List<Guid>? EquipmentIds, bool IsActive);
}
