using KiteFlow.BuildingBlocks.MultiTenancy;

namespace KiteFlow.Services.Academics.Api.Domain;

public sealed class Lesson : TenantScopedEntity
{
    public Guid StudentId { get; set; }

    public Guid InstructorId { get; set; }

    public LessonKind Kind { get; set; }

    public LessonStatus Status { get; set; } = LessonStatus.Scheduled;

    public Guid? EnrollmentId { get; set; }

    public decimal? SingleLessonPrice { get; set; }

    public DateTime StartAtUtc { get; set; }

    public int DurationMinutes { get; set; }

    public string? Notes { get; set; }

    public DateTime? OperationalConfirmedAtUtc { get; set; }

    public Guid? OperationalConfirmedByUserId { get; set; }

    public string? OperationalConfirmationNote { get; set; }

    public DateTime? StudentConfirmedAtUtc { get; set; }

    public string? StudentConfirmationNote { get; set; }

    public DateTime? NoShowMarkedAtUtc { get; set; }

    public Guid? NoShowMarkedByUserId { get; set; }

    public string? NoShowNote { get; set; }

    public Student? Student { get; set; }

    public Instructor? Instructor { get; set; }

    public Enrollment? Enrollment { get; set; }
}
