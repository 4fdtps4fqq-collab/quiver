using KiteFlow.BuildingBlocks.MultiTenancy;

namespace KiteFlow.Services.Academics.Api.Domain;

public sealed class Course : TenantScopedEntity
{
    public string Name { get; set; } = string.Empty;

    public CourseLevel Level { get; set; }

    public int TotalMinutes { get; set; }

    public decimal Price { get; set; }

    public bool IsActive { get; set; } = true;
}
