using KiteFlow.BuildingBlocks.MultiTenancy;

namespace KiteFlow.Services.Equipment.Api.Domain;

public sealed class MaintenanceRule : TenantScopedEntity
{
    public EquipmentType EquipmentType { get; set; }

    public int? ServiceEveryMinutes { get; set; }

    public int? ServiceEveryDays { get; set; }

    public bool IsActive { get; set; } = true;
}
