using KiteFlow.BuildingBlocks.MultiTenancy;

namespace KiteFlow.Services.Academics.Api.Domain;

public sealed class StudentPortalNotification : TenantScopedEntity
{
    public Guid StudentId { get; set; }

    public Guid? LessonId { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? ActionLabel { get; set; }

    public string? ActionPath { get; set; }

    public DateTime? ReadAtUtc { get; set; }

}
