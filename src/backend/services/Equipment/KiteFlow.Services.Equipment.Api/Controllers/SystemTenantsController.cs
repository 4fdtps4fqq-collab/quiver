using KiteFlow.Services.Equipment.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Equipment.Api.Controllers;

[ApiController]
[Authorize(Policy = "SystemAdminOnly")]
[Route("api/v1/system/tenants")]
public sealed class SystemTenantsController : ControllerBase
{
    private readonly EquipmentDbContext _dbContext;

    public SystemTenantsController(EquipmentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpDelete("{schoolId:guid}")]
    public async Task<IActionResult> DeleteTenant(Guid schoolId)
    {
        var maintenanceRecords = await _dbContext.MaintenanceRecords.Where(x => x.SchoolId == schoolId).ToListAsync();
        var usageLogs = await _dbContext.EquipmentUsageLogs.Where(x => x.SchoolId == schoolId).ToListAsync();
        var checkoutItems = await _dbContext.LessonEquipmentCheckoutItems.Where(x => x.SchoolId == schoolId).ToListAsync();
        var checkouts = await _dbContext.LessonEquipmentCheckouts.Where(x => x.SchoolId == schoolId).ToListAsync();
        var rules = await _dbContext.MaintenanceRules.Where(x => x.SchoolId == schoolId).ToListAsync();
        var items = await _dbContext.EquipmentItems.Where(x => x.SchoolId == schoolId).ToListAsync();
        var storages = await _dbContext.GearStorages.Where(x => x.SchoolId == schoolId).ToListAsync();

        if (maintenanceRecords.Count > 0) _dbContext.MaintenanceRecords.RemoveRange(maintenanceRecords);
        if (usageLogs.Count > 0) _dbContext.EquipmentUsageLogs.RemoveRange(usageLogs);
        if (checkoutItems.Count > 0) _dbContext.LessonEquipmentCheckoutItems.RemoveRange(checkoutItems);
        if (checkouts.Count > 0) _dbContext.LessonEquipmentCheckouts.RemoveRange(checkouts);
        if (rules.Count > 0) _dbContext.MaintenanceRules.RemoveRange(rules);
        if (items.Count > 0) _dbContext.EquipmentItems.RemoveRange(items);
        if (storages.Count > 0) _dbContext.GearStorages.RemoveRange(storages);

        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            deletedAtUtc = DateTime.UtcNow,
            schoolId
        });
    }
}
