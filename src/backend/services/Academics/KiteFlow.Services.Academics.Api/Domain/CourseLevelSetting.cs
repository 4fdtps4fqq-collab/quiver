using KiteFlow.BuildingBlocks.MultiTenancy;

namespace KiteFlow.Services.Academics.Api.Domain;

public sealed class CourseLevelSetting : TenantScopedEntity
{
    public int LevelValue { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public string PedagogicalTrackJson { get; set; } = "[]";
}
