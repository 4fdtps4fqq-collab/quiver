using KiteFlow.BuildingBlocks.MultiTenancy;

namespace KiteFlow.Services.Finance.Api.Domain;

public sealed class FinancialCategory : TenantScopedEntity
{
    public string Name { get; set; } = string.Empty;

    public FinancialCategoryDirection Direction { get; set; } = FinancialCategoryDirection.Both;

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}
