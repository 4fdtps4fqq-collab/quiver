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
    public async Task<IActionResult> GetAll(
        [FromQuery] EquipmentType? type,
        [FromQuery] EquipmentCondition? condition,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            return BadRequest("A data inicial deve ser menor ou igual à data final.");
        }

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
            .Select(x => new EquipmentListRow(
                x.Id,
                x.Name,
                x.Type.ToString(),
                x.Category,
                x.TagCode,
                x.Brand,
                x.Model,
                x.SizeLabel,
                x.CurrentCondition.ToString(),
                x.TotalUsageMinutes,
                x.LastServiceDateUtc,
                x.LastServiceUsageMinutes,
                x.IsActive,
                x.StorageId,
                x.Storage!.Name,
                x.OwnershipType.ToString(),
                x.OwnerDisplayName))
            .ToListAsync();

        var equipmentIds = items.Select(x => x.Id).ToList();
        var reservations = await GetReservationWindowsAsync(schoolId, equipmentIds, fromUtc, toUtc);
        var openCheckoutEquipmentIds = await GetOpenCheckoutEquipmentIdsAsync(schoolId, equipmentIds);
        var kitLinks = await GetKitLinksAsync(schoolId, equipmentIds);

        var payload = items.Select(item =>
        {
            var reservation = reservations.FirstOrDefault(x => x.EquipmentId == item.Id);
            var availabilityStatus = ResolveAvailabilityStatus(item, reservation, openCheckoutEquipmentIds.Contains(item.Id));
            var linkedKit = kitLinks.FirstOrDefault(x => x.EquipmentId == item.Id);

            return new
            {
                item.Id,
                item.Name,
                item.Type,
                item.Category,
                item.TagCode,
                item.Brand,
                item.Model,
                item.SizeLabel,
                condition = item.Condition,
                item.TotalUsageMinutes,
                item.LastServiceDateUtc,
                item.LastServiceUsageMinutes,
                item.IsActive,
                item.StorageId,
                storageName = item.StorageName,
                item.OwnershipType,
                item.OwnerDisplayName,
                totalUsageHours = Math.Round(item.TotalUsageMinutes / 60m, 2),
                availabilityStatus,
                reservedLessonId = reservation?.LessonId,
                reservedFromUtc = reservation?.ReservedFromUtc,
                reservedUntilUtc = reservation?.ReservedUntilUtc,
                reservedLessonLabel = reservation is null ? null : $"Reserva até {reservation.ReservedUntilUtc:dd/MM HH:mm}",
                isCheckedOut = openCheckoutEquipmentIds.Contains(item.Id),
                kitId = linkedKit?.KitId,
                kitName = linkedKit?.KitName
            };
        });

        return Ok(payload);
    }

    [HttpGet("availability")]
    public async Task<IActionResult> GetAvailability([FromQuery] DateTime fromUtc, [FromQuery] DateTime toUtc)
    {
        if (fromUtc > toUtc)
        {
            return BadRequest("A data inicial deve ser menor ou igual à data final.");
        }

        return await GetAll(null, null, fromUtc, toUtc);
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

        var linkedKit = await _dbContext.EquipmentKitItems
            .Where(x => x.SchoolId == schoolId && x.EquipmentId == id)
            .Join(_dbContext.EquipmentKits.Where(x => x.SchoolId == schoolId && x.IsActive),
                item => item.KitId,
                kit => kit.Id,
                (item, kit) => new { kit.Id, kit.Name })
            .FirstOrDefaultAsync();

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
                x.PerformedBy,
                serviceCategory = x.ServiceCategory.ToString(),
                financialEffect = x.FinancialEffect.ToString(),
                x.CounterpartyName
            })
            .ToListAsync();

        var reservations = await _dbContext.EquipmentReservationItems
            .Where(x => x.SchoolId == schoolId && x.EquipmentId == id)
            .Join(_dbContext.EquipmentReservations.Where(x => x.SchoolId == schoolId),
                item => item.ReservationId,
                reservation => reservation.Id,
                (_, reservation) => new
                {
                    reservation.Id,
                    reservation.LessonId,
                    reservation.ReservedFromUtc,
                    reservation.ReservedUntilUtc,
                    reservation.Notes
                })
            .OrderByDescending(x => x.ReservedFromUtc)
            .ToListAsync();

        var maintenanceExpense = maintenance
            .Where(x => x.financialEffect == MaintenanceFinancialEffect.Expense.ToString())
            .Sum(x => x.Cost ?? 0m);

        var maintenanceRevenue = maintenance
            .Where(x => x.financialEffect == MaintenanceFinancialEffect.Revenue.ToString())
            .Sum(x => x.Cost ?? 0m);

        var timeline = usage
            .Select(x => new EquipmentTimelineItem(x.RecordedAtUtc, "Uso", $"{x.UsageMinutes} min em aula", x.conditionAfter))
            .Concat(maintenance.Select(x => new EquipmentTimelineItem(
                x.ServiceDateUtc,
                "Manutenção",
                x.Description,
                x.serviceCategory)))
            .Concat(reservations.Select(x => new EquipmentTimelineItem(
                x.ReservedFromUtc,
                "Reserva",
                $"Reserva da aula {x.LessonId}",
                x.ReservedUntilUtc.ToString("dd/MM HH:mm"))))
            .OrderByDescending(x => x.AtUtc)
            .ToList();

        return Ok(new
        {
            equipment = new
            {
                equipment.Id,
                equipment.Name,
                type = equipment.Type.ToString(),
                equipment.Category,
                equipment.TagCode,
                equipment.Brand,
                equipment.Model,
                equipment.SizeLabel,
                condition = equipment.CurrentCondition.ToString(),
                equipment.TotalUsageMinutes,
                equipment.LastServiceDateUtc,
                equipment.LastServiceUsageMinutes,
                equipment.StorageId,
                storageName = equipment.Storage!.Name,
                ownershipType = equipment.OwnershipType.ToString(),
                equipment.OwnerDisplayName,
                kitId = linkedKit?.Id,
                kitName = linkedKit?.Name
            },
            usage,
            maintenance,
            reservations,
            lifecycle = new
            {
                usageMinutes = usage.Sum(x => x.UsageMinutes),
                servicesCount = maintenance.Count,
                reservationsCount = reservations.Count,
                maintenanceExpense,
                maintenanceRevenue,
                timeline
            }
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
            Category = NormalizeNullable(request.Category),
            TagCode = NormalizeNullable(request.TagCode),
            Brand = NormalizeNullable(request.Brand),
            Model = NormalizeNullable(request.Model),
            SizeLabel = NormalizeNullable(request.SizeLabel),
            CurrentCondition = request.CurrentCondition,
            OwnershipType = request.OwnershipType,
            OwnerDisplayName = NormalizeNullable(request.OwnerDisplayName),
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
        item.Category = NormalizeNullable(request.Category);
        item.TagCode = NormalizeNullable(request.TagCode);
        item.Brand = NormalizeNullable(request.Brand);
        item.Model = NormalizeNullable(request.Model);
        item.SizeLabel = NormalizeNullable(request.SizeLabel);
        item.CurrentCondition = request.CurrentCondition;
        item.OwnershipType = request.OwnershipType;
        item.OwnerDisplayName = NormalizeNullable(request.OwnerDisplayName);
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

        return storageExists ? null : BadRequest("O depósito informado não é válido para a escola atual.");
    }

    private async Task<List<ReservationWindow>> GetReservationWindowsAsync(
        Guid schoolId,
        List<Guid> equipmentIds,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        if (equipmentIds.Count == 0 || !fromUtc.HasValue || !toUtc.HasValue)
        {
            return [];
        }

        return await _dbContext.EquipmentReservationItems
            .Where(x => x.SchoolId == schoolId && equipmentIds.Contains(x.EquipmentId))
            .Join(_dbContext.EquipmentReservations.Where(x =>
                    x.SchoolId == schoolId &&
                    x.ReservedFromUtc < toUtc.Value &&
                    x.ReservedUntilUtc > fromUtc.Value),
                item => item.ReservationId,
                reservation => reservation.Id,
                (item, reservation) => new ReservationWindow(
                    item.EquipmentId,
                    reservation.LessonId,
                    reservation.ReservedFromUtc,
                    reservation.ReservedUntilUtc))
            .ToListAsync();
    }

    private async Task<HashSet<Guid>> GetOpenCheckoutEquipmentIdsAsync(Guid schoolId, List<Guid> equipmentIds)
    {
        if (equipmentIds.Count == 0)
        {
            return [];
        }

        var ids = await _dbContext.LessonEquipmentCheckoutItems
            .Where(x => x.SchoolId == schoolId && equipmentIds.Contains(x.EquipmentId))
            .Join(_dbContext.LessonEquipmentCheckouts.Where(x => x.SchoolId == schoolId && x.CheckedInAtUtc == null),
                item => item.CheckoutId,
                checkout => checkout.Id,
                (item, _) => item.EquipmentId)
            .Distinct()
            .ToListAsync();

        return ids.ToHashSet();
    }

    private async Task<List<KitLink>> GetKitLinksAsync(Guid schoolId, List<Guid> equipmentIds)
    {
        if (equipmentIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.EquipmentKitItems
            .Where(x => x.SchoolId == schoolId && equipmentIds.Contains(x.EquipmentId))
            .Join(_dbContext.EquipmentKits.Where(x => x.SchoolId == schoolId && x.IsActive),
                item => item.KitId,
                kit => kit.Id,
                (item, kit) => new KitLink(item.EquipmentId, kit.Id, kit.Name))
            .ToListAsync();
    }

    private static string ResolveAvailabilityStatus(EquipmentListRow item, ReservationWindow? reservation, bool isCheckedOut)
    {
        if (!item.IsActive)
        {
            return "Inactive";
        }

        if (item.Condition is nameof(EquipmentCondition.OutOfService) or nameof(EquipmentCondition.NeedsRepair))
        {
            return "MaintenanceBlocked";
        }

        if (isCheckedOut)
        {
            return "CheckedOut";
        }

        if (reservation is not null)
        {
            return "Reserved";
        }

        return "Available";
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record EquipmentListRow(
        Guid Id,
        string Name,
        string Type,
        string? Category,
        string? TagCode,
        string? Brand,
        string? Model,
        string? SizeLabel,
        string Condition,
        int TotalUsageMinutes,
        DateTime? LastServiceDateUtc,
        int? LastServiceUsageMinutes,
        bool IsActive,
        Guid StorageId,
        string StorageName,
        string OwnershipType,
        string? OwnerDisplayName);

    private sealed record ReservationWindow(Guid EquipmentId, Guid LessonId, DateTime ReservedFromUtc, DateTime ReservedUntilUtc);

    private sealed record KitLink(Guid EquipmentId, Guid KitId, string KitName);

    private sealed record EquipmentTimelineItem(DateTime AtUtc, string Kind, string Title, string Detail);

    public sealed record UpsertEquipmentRequest(
        Guid StorageId,
        string Name,
        EquipmentType Type,
        string? Category,
        string? TagCode,
        string? Brand,
        string? Model,
        string? SizeLabel,
        EquipmentCondition CurrentCondition,
        EquipmentOwnershipType OwnershipType,
        string? OwnerDisplayName);

    public sealed record UpdateEquipmentRequest(
        Guid StorageId,
        string Name,
        EquipmentType Type,
        string? Category,
        string? TagCode,
        string? Brand,
        string? Model,
        string? SizeLabel,
        EquipmentCondition CurrentCondition,
        EquipmentOwnershipType OwnershipType,
        string? OwnerDisplayName,
        bool IsActive);
}
