using KiteFlow.BuildingBlocks.MultiTenancy;

namespace KiteFlow.Services.Equipment.Api.Domain;

public sealed class GearStorage : TenantScopedEntity
{
    public string Name { get; set; } = string.Empty;

    public string? LocationNote { get; set; }

    public bool IsActive { get; set; } = true;
}
