using KiteFlow.BuildingBlocks.MultiTenancy;
using KiteFlow.Services.Equipment.Api.Data;
using KiteFlow.Services.Equipment.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Equipment.Api.Controllers;

[ApiController]
[Authorize(Policy = "EquipmentAccess")]
[Route("api/v1/storages")]
public sealed class StoragesController : ControllerBase
{
    private readonly EquipmentDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;

    public StoragesController(EquipmentDbContext dbContext, ICurrentTenant currentTenant)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var items = await _dbContext.GearStorages
            .Where(x => x.SchoolId == schoolId)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.LocationNote,
                x.IsActive
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertStorageRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest("O nome do depósito é obrigatório.");
        }

        var exists = await _dbContext.GearStorages.AnyAsync(x => x.SchoolId == schoolId && x.Name == name);
        if (exists)
        {
            return Conflict("Storage already exists for this school.");
        }

        var storage = new GearStorage
        {
            SchoolId = schoolId,
            Name = name,
            LocationNote = NormalizeNullable(request.LocationNote),
            IsActive = true
        };

        _dbContext.GearStorages.Add(storage);
        await _dbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { id = storage.Id }, new { storageId = storage.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStorageRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var storage = await _dbContext.GearStorages.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
        if (storage is null)
        {
            return NotFound();
        }

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest("O nome do depósito é obrigatório.");
        }

        var duplicate = await _dbContext.GearStorages.AnyAsync(x =>
            x.SchoolId == schoolId &&
            x.Name == name &&
            x.Id != id);

        if (duplicate)
        {
            return Conflict("Storage already exists for this school.");
        }

        storage.Name = name;
        storage.LocationNote = NormalizeNullable(request.LocationNote);
        storage.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync();
        return Ok();
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public sealed record UpsertStorageRequest(string Name, string? LocationNote);

    public sealed record UpdateStorageRequest(string Name, string? LocationNote, bool IsActive);
}
