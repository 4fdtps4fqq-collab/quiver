using KiteFlow.BuildingBlocks.MultiTenancy;

namespace KiteFlow.Services.Academics.Api.Domain;

public sealed class ScheduleBlock : TenantScopedEntity
{
    public ScheduleBlockScope Scope { get; set; } = ScheduleBlockScope.School;

    public Guid? InstructorId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public DateTime StartAtUtc { get; set; }

    public DateTime EndAtUtc { get; set; }

    public Instructor? Instructor { get; set; }
}
