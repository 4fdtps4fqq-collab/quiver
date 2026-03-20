using KiteFlow.BuildingBlocks.MultiTenancy;

namespace KiteFlow.Services.Equipment.Api.Domain;

public sealed class EquipmentKit : TenantScopedEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}
