using System.Text.Json;
using KiteFlow.Services.Academics.Api.Data;
using KiteFlow.Services.Academics.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Academics.Api.Services;

public sealed class LessonSchedulingService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AcademicsDbContext _dbContext;
    private readonly SchoolOperationsSettingsClient _settingsClient;

    public LessonSchedulingService(AcademicsDbContext dbContext, SchoolOperationsSettingsClient settingsClient)
    {
        _dbContext = dbContext;
        _settingsClient = settingsClient;
    }

    public async Task<ScheduleValidationResult> ValidateAsync(
        Guid schoolId,
        Lesson lesson,
        Guid? currentLessonId = null,
        CancellationToken cancellationToken = default)
    {
        var schedulingStatuses = new[]
        {
            LessonStatus.Scheduled,
            LessonStatus.Confirmed,
            LessonStatus.Realized,
            LessonStatus.NoShow
        };

        var settings = await _settingsClient.GetAsync(schoolId, cancellationToken);
        var lessonStart = lesson.StartAtUtc;
        var lessonEnd = lesson.StartAtUtc.AddMinutes(lesson.DurationMinutes);

        if (lessonEnd <= lessonStart)
        {
            return ScheduleValidationResult.Invalid("A aula precisa terminar depois do horário de início.");
        }

        var instructor = await _dbContext.Instructors
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == lesson.InstructorId && x.SchoolId == schoolId, cancellationToken);

        if (instructor is null)
        {
            return ScheduleValidationResult.Invalid("O instrutor informado não foi encontrado.");
        }

        var bufferStart = lessonStart.AddMinutes(-settings.InstructorBufferMinutes);
        var bufferEnd = lessonEnd.AddMinutes(settings.InstructorBufferMinutes);

        var studentConflict = await _dbContext.Lessons.AnyAsync(x =>
            x.SchoolId == schoolId &&
            x.Id != currentLessonId &&
            x.StudentId == lesson.StudentId &&
            schedulingStatuses.Contains(x.Status) &&
            x.StartAtUtc < lessonEnd &&
            x.StartAtUtc.AddMinutes(x.DurationMinutes) > lessonStart,
            cancellationToken);

        if (studentConflict)
        {
            return ScheduleValidationResult.Invalid("O aluno já possui outra aula conflitante nesse intervalo.");
        }

        var instructorConflict = await _dbContext.Lessons.AnyAsync(x =>
            x.SchoolId == schoolId &&
            x.Id != currentLessonId &&
            x.InstructorId == lesson.InstructorId &&
            schedulingStatuses.Contains(x.Status) &&
            x.StartAtUtc < bufferEnd &&
            x.StartAtUtc.AddMinutes(x.DurationMinutes) > bufferStart,
            cancellationToken);

        if (instructorConflict)
        {
            return ScheduleValidationResult.Invalid("O instrutor já possui outra aula ou buffer ocupado nesse intervalo.");
        }

        var scheduleBlockConflict = await _dbContext.ScheduleBlocks.AnyAsync(x =>
            x.SchoolId == schoolId &&
            x.Id != currentLessonId &&
            x.StartAtUtc < lessonEnd &&
            x.EndAtUtc > lessonStart &&
            (x.Scope == ScheduleBlockScope.School || x.InstructorId == lesson.InstructorId),
            cancellationToken);

        if (scheduleBlockConflict)
        {
            return ScheduleValidationResult.Invalid("Existe um bloqueio de agenda para esse horário.");
        }

        var availabilityCheck = ValidateInstructorAvailability(instructor.AvailabilityJson, lessonStart, lessonEnd);
        if (!availabilityCheck.IsValid)
        {
            return availabilityCheck;
        }

        return ScheduleValidationResult.Valid(settings);
    }

    public async Task<IReadOnlyList<LessonSuggestionSlot>> GetSuggestedSlotsAsync(
        Guid schoolId,
        Lesson lesson,
        DateTime searchStartAtUtc,
        int daysToSearch,
        Guid? preferredInstructorId,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsClient.GetAsync(schoolId, cancellationToken);
        var instructorId = preferredInstructorId ?? lesson.InstructorId;
        var instructor = await _dbContext.Instructors
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == instructorId && x.SchoolId == schoolId, cancellationToken);

        if (instructor is null)
        {
            return [];
        }

        var slots = ParseAvailability(instructor.AvailabilityJson);
        if (slots.Count == 0)
        {
            return [];
        }

        var suggestions = new List<LessonSuggestionSlot>();
        var cursorDate = searchStartAtUtc.Date;
        var endDate = searchStartAtUtc.Date.AddDays(Math.Max(1, daysToSearch));

        while (cursorDate <= endDate && suggestions.Count < 8)
        {
            foreach (var slot in slots.Where(x => x.DayOfWeek == cursorDate.DayOfWeek))
            {
                var dayWindowStart = cursorDate.AddMinutes(slot.StartMinutesUtc);
                var dayWindowEnd = cursorDate.AddMinutes(slot.EndMinutesUtc);
                var current = dayWindowStart < searchStartAtUtc ? RoundUpToHalfHour(searchStartAtUtc) : dayWindowStart;

                while (current.AddMinutes(lesson.DurationMinutes) <= dayWindowEnd && suggestions.Count < 8)
                {
                    var candidate = new Lesson
                    {
                        SchoolId = schoolId,
                        StudentId = lesson.StudentId,
                        InstructorId = instructorId,
                        Kind = lesson.Kind,
                        Status = LessonStatus.Scheduled,
                        EnrollmentId = lesson.EnrollmentId,
                        SingleLessonPrice = lesson.SingleLessonPrice,
                        StartAtUtc = current,
                        DurationMinutes = lesson.DurationMinutes,
                        Notes = lesson.Notes
                    };

                    var validation = await ValidateAsync(schoolId, candidate, lesson.Id, cancellationToken);
                    if (validation.IsValid)
                    {
                        suggestions.Add(new LessonSuggestionSlot(
                            current,
                            current.AddMinutes(lesson.DurationMinutes),
                            instructorId,
                            instructor.FullName,
                            slot.Label));
                    }

                    current = current.AddMinutes(30);
                }
            }

            cursorDate = cursorDate.AddDays(1);
        }

        return suggestions;
    }

    public static IReadOnlyList<InstructorAvailabilitySlotModel> ParseAvailability(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return GetDefaultAvailability();
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<InstructorAvailabilitySlotModel>>(json, JsonOptions);
            return items?.Where(IsValidSlot).OrderBy(x => x.DayOfWeek).ThenBy(x => x.StartMinutesUtc).ToArray()
                   ?? GetDefaultAvailability();
        }
        catch
        {
            return GetDefaultAvailability();
        }
    }

    public static string SerializeAvailability(IEnumerable<InstructorAvailabilitySlotModel>? slots)
    {
        var normalized = (slots ?? [])
            .Where(IsValidSlot)
            .OrderBy(x => x.DayOfWeek)
            .ThenBy(x => x.StartMinutesUtc)
            .ToArray();

        return JsonSerializer.Serialize(normalized, JsonOptions);
    }

    private static ScheduleValidationResult ValidateInstructorAvailability(
        string? availabilityJson,
        DateTime lessonStart,
        DateTime lessonEnd)
    {
        var slots = ParseAvailability(availabilityJson);
        if (slots.Count == 0)
        {
            return ScheduleValidationResult.Invalid("O instrutor não possui disponibilidade cadastrada.");
        }

        var dayOfWeek = lessonStart.DayOfWeek;
        var startMinutes = lessonStart.Hour * 60 + lessonStart.Minute;
        var endMinutes = lessonEnd.Hour * 60 + lessonEnd.Minute;
        var matches = slots.Any(x =>
            x.DayOfWeek == dayOfWeek &&
            startMinutes >= x.StartMinutesUtc &&
            endMinutes <= x.EndMinutesUtc);

        return matches
            ? ScheduleValidationResult.Valid()
            : ScheduleValidationResult.Invalid("O horário escolhido está fora da disponibilidade cadastrada do instrutor.");
    }

    private static bool IsValidSlot(InstructorAvailabilitySlotModel slot)
        => slot.StartMinutesUtc >= 0 &&
           slot.EndMinutesUtc > slot.StartMinutesUtc &&
           slot.EndMinutesUtc <= 24 * 60;

    private static DateTime RoundUpToHalfHour(DateTime value)
    {
        var minuteBlock = value.Minute <= 30 ? 30 : 60;
        var rounded = new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0, DateTimeKind.Utc)
            .AddMinutes(minuteBlock);
        return rounded <= value ? rounded.AddMinutes(30) : rounded;
    }

    private static IReadOnlyList<InstructorAvailabilitySlotModel> GetDefaultAvailability()
        =>
        [
            new(DayOfWeek.Monday, 8 * 60, 18 * 60, "Segunda"),
            new(DayOfWeek.Tuesday, 8 * 60, 18 * 60, "Terça"),
            new(DayOfWeek.Wednesday, 8 * 60, 18 * 60, "Quarta"),
            new(DayOfWeek.Thursday, 8 * 60, 18 * 60, "Quinta"),
            new(DayOfWeek.Friday, 8 * 60, 18 * 60, "Sexta"),
            new(DayOfWeek.Saturday, 8 * 60, 14 * 60, "Sábado")
        ];
}

public sealed record InstructorAvailabilitySlotModel(
    DayOfWeek DayOfWeek,
    int StartMinutesUtc,
    int EndMinutesUtc,
    string Label);

public sealed record LessonSuggestionSlot(
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    Guid InstructorId,
    string InstructorName,
    string AvailabilityLabel);

public sealed record ScheduleValidationResult(bool IsValid, string? ErrorMessage, SchoolOperationsSettings? Settings)
{
    public static ScheduleValidationResult Valid(SchoolOperationsSettings? settings = null) => new(true, null, settings);

    public static ScheduleValidationResult Invalid(string message) => new(false, message, null);
}
