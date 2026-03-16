using KiteFlow.BuildingBlocks.MultiTenancy;

namespace KiteFlow.Services.Academics.Api.Domain;

public sealed class EnrollmentBalanceLedgerEntry : TenantScopedEntity
{
    public Guid EnrollmentId { get; set; }

    public Guid? LessonId { get; set; }

    public int DeltaMinutes { get; set; }

    public string Reason { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public Enrollment? Enrollment { get; set; }
}
