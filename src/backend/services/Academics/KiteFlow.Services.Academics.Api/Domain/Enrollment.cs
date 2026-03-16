using KiteFlow.BuildingBlocks.MultiTenancy;

namespace KiteFlow.Services.Academics.Api.Domain;

public sealed class Enrollment : TenantScopedEntity
{
    public Guid StudentId { get; set; }

    public Guid CourseId { get; set; }

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;

    public int IncludedMinutesSnapshot { get; set; }

    public int UsedMinutes { get; set; }

    public decimal CoursePriceSnapshot { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public DateTime? EndedAtUtc { get; set; }

    public Student? Student { get; set; }

    public Course? Course { get; set; }
}
