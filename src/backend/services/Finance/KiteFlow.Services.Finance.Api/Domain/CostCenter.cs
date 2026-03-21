using KiteFlow.BuildingBlocks.MultiTenancy;

namespace KiteFlow.Services.Finance.Api.Domain;

public sealed class CostCenter : TenantScopedEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}
