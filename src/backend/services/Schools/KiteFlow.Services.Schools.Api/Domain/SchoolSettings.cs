namespace KiteFlow.Services.Schools.Api.Domain;

public sealed class SchoolSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SchoolId { get; set; }

    public int BookingLeadTimeMinutes { get; set; } = 60;

    public int CancellationWindowHours { get; set; } = 24;

    public int RescheduleWindowHours { get; set; } = 24;

    public int AttendanceConfirmationLeadMinutes { get; set; } = 180;

    public int LessonReminderLeadHours { get; set; } = 18;

    public bool PortalNotificationsEnabled { get; set; } = true;

    public int InstructorBufferMinutes { get; set; } = 15;

    public int NoShowGraceMinutes { get; set; } = 15;

    public bool NoShowConsumesCourseMinutes { get; set; } = true;

    public bool NoShowChargesSingleLesson { get; set; } = true;

    public bool AutoCreateEnrollmentRevenue { get; set; } = true;

    public bool AutoCreateSingleLessonRevenue { get; set; } = true;

    public string ThemePrimary { get; set; } = "#0E3A52";

    public string ThemeAccent { get; set; } = "#FFB703";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public School? School { get; set; }
}
