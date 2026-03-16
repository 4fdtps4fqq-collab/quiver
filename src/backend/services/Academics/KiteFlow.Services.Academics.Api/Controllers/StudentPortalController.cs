using System.Net.Http.Json;
using System.Security.Claims;
using KiteFlow.BuildingBlocks.MultiTenancy;
using KiteFlow.Services.Academics.Api.Data;
using KiteFlow.Services.Academics.Api.Domain;
using KiteFlow.Services.Academics.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Academics.Api.Controllers;

[ApiController]
[Authorize(Policy = "StudentOnly")]
[Route("api/v1/student-portal")]
public sealed class StudentPortalController : ControllerBase
{
    private static readonly LessonStatus[] BlockingStatuses =
    [
        LessonStatus.Scheduled,
        LessonStatus.Confirmed,
        LessonStatus.Realized,
        LessonStatus.NoShow
    ];

    private static readonly PortalSchoolSettings DefaultPortalSettings = new(
        BookingLeadTimeMinutes: 60,
        CancellationWindowHours: 24,
        RescheduleWindowHours: 24,
        AttendanceConfirmationLeadMinutes: 180,
        LessonReminderLeadHours: 18,
        PortalNotificationsEnabled: true,
        ThemePrimary: "#0E3A52",
        ThemeAccent: "#FFB703");

    private readonly AcademicsDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly LessonSchedulingService _lessonSchedulingService;

    public StudentPortalController(
        AcademicsDbContext dbContext,
        ICurrentTenant currentTenant,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        LessonSchedulingService lessonSchedulingService)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _lessonSchedulingService = lessonSchedulingService;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview()
    {
        var access = await ResolveStudentAccessAsync();
        if (access.Error is not null)
        {
            return access.Error;
        }

        var schoolId = access.SchoolId;
        var student = access.Student!;
        var now = DateTime.UtcNow;
        var settings = await GetPortalSettingsAsync();

        var enrollments = await _dbContext.Enrollments
            .Where(x => x.SchoolId == schoolId && x.StudentId == student.Id)
            .Include(x => x.Course)
            .OrderByDescending(x => x.StartedAtUtc)
            .ToListAsync();

        var lessons = await _dbContext.Lessons
            .Where(x => x.SchoolId == schoolId && x.StudentId == student.Id)
            .Include(x => x.Instructor)
            .Include(x => x.Enrollment)
            .ThenInclude(x => x!.Course)
            .OrderBy(x => x.StartAtUtc)
            .ToListAsync();

        var instructors = await _dbContext.Instructors
            .Where(x => x.SchoolId == schoolId && x.IsActive)
            .OrderBy(x => x.FullName)
            .Select(x => new
            {
                x.Id,
                x.FullName,
                x.Specialties
            })
            .ToListAsync();

        var scheduledCounts = lessons
            .Where(x =>
                x.EnrollmentId.HasValue &&
                x.StartAtUtc >= now &&
                (x.Status == LessonStatus.Scheduled || x.Status == LessonStatus.Confirmed))
            .GroupBy(x => x.EnrollmentId!.Value)
            .ToDictionary(x => x.Key, x => x.Sum(lesson => lesson.DurationMinutes));

        var upcomingLessons = lessons
            .Where(x => x.StartAtUtc >= now && (x.Status == LessonStatus.Scheduled || x.Status == LessonStatus.Confirmed))
            .Take(12)
            .Select(x => new
            {
                x.Id,
                kind = x.Kind.ToString(),
                status = x.Status.ToString(),
                x.StartAtUtc,
                x.DurationMinutes,
                x.Notes,
                x.StudentConfirmedAtUtc,
                x.StudentConfirmationNote,
                minutesUntilStart = (int)Math.Round((x.StartAtUtc - now).TotalMinutes),
                canCancel = CanCancelLesson(x, now, settings),
                canReschedule = CanRescheduleLesson(x, now, settings),
                canConfirmPresence = CanConfirmPresence(x, now, settings),
                instructor = new
                {
                    instructorId = x.InstructorId,
                    name = x.Instructor!.FullName
                },
                enrollment = x.EnrollmentId.HasValue && x.Enrollment != null
                    ? new
                    {
                        enrollmentId = x.EnrollmentId,
                        x.Enrollment.CourseId,
                        courseName = x.Enrollment.Course!.Name
                    }
                    : null
            })
            .ToList();

        var lessonHistory = lessons
            .OrderByDescending(x => x.StartAtUtc)
            .Take(20)
            .Select(x => new
            {
                x.Id,
                kind = x.Kind.ToString(),
                status = x.Status.ToString(),
                x.StartAtUtc,
                x.DurationMinutes,
                x.Notes,
                x.StudentConfirmedAtUtc,
                x.StudentConfirmationNote,
                instructorName = x.Instructor!.FullName,
                courseName = x.Enrollment?.Course?.Name,
                sessionTitle = ResolveSessionTitle(x),
                evolutionSummary = ResolveEvolutionSummary(x),
                statusMessage = ResolveHistoryStatusMessage(x)
            })
            .ToList();

        var realizedLessons = lessons
            .Where(x => x.Status == LessonStatus.Realized)
            .OrderBy(x => x.StartAtUtc)
            .ToList();

        var completedCourses = enrollments.Count(x => x.Status == EnrollmentStatus.Completed);
        var trainingProgress = BuildTrainingProgress(student, realizedLessons, enrollments, completedCourses);
        var notifications = await LoadPortalNotificationsAsync(student.Id, settings, upcomingLessons.Cast<object>().ToList());
        var profileCompleteness = CalculateProfileCompleteness(student);

        return Ok(new
        {
            student = new
            {
                student.Id,
                student.FullName,
                student.Email,
                student.Phone,
                student.BirthDate,
                student.EmergencyContactName,
                student.EmergencyContactPhone,
                student.FirstStandUpAtUtc
            },
            summary = new
            {
                totalRealizedLessons = realizedLessons.Count,
                totalUpcomingLessons = upcomingLessons.Count,
                activeEnrollments = enrollments.Count(x => x.Status == EnrollmentStatus.Active),
                completedCourses,
                trainingStage = ResolveTrainingStage(student.FirstStandUpAtUtc, realizedLessons.Count),
                profileCompleteness
            },
            portalRules = new
            {
                settings.BookingLeadTimeMinutes,
                settings.CancellationWindowHours,
                settings.RescheduleWindowHours,
                settings.AttendanceConfirmationLeadMinutes,
                settings.LessonReminderLeadHours,
                settings.PortalNotificationsEnabled
            },
            progress = trainingProgress,
            notifications = new
            {
                unreadCount = notifications.Count(x => x.ReadAtUtc is null),
                items = notifications.Take(6)
            },
            enrollments = enrollments.Select(x =>
            {
                scheduledCounts.TryGetValue(x.Id, out var scheduledMinutes);
                var remainingMinutes = Math.Max(0, x.IncludedMinutesSnapshot - x.UsedMinutes);
                var availableToScheduleMinutes = Math.Max(0, remainingMinutes - scheduledMinutes);

                return new
                {
                    x.Id,
                    x.CourseId,
                    courseName = x.Course!.Name,
                    level = x.Course.Level.ToString(),
                    status = x.Status.ToString(),
                    x.IncludedMinutesSnapshot,
                    x.UsedMinutes,
                    remainingMinutes,
                    scheduledMinutes,
                    availableToScheduleMinutes,
                    progressPercent = x.IncludedMinutesSnapshot == 0
                        ? 0
                        : Math.Round((decimal)x.UsedMinutes / x.IncludedMinutesSnapshot * 100, 1),
                    x.StartedAtUtc,
                    x.EndedAtUtc
                };
            }),
            upcomingLessons,
            lessonHistory,
            instructors
        });
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory()
    {
        var access = await ResolveStudentAccessAsync();
        if (access.Error is not null)
        {
            return access.Error;
        }

        var schoolId = access.SchoolId;
        var student = access.Student!;

        var lessons = await _dbContext.Lessons
            .Where(x => x.SchoolId == schoolId && x.StudentId == student.Id)
            .Include(x => x.Instructor)
            .Include(x => x.Enrollment)
            .ThenInclude(x => x!.Course)
            .OrderByDescending(x => x.StartAtUtc)
            .Take(40)
            .ToListAsync();

        return Ok(new
        {
            student = new
            {
                student.Id,
                student.FullName
            },
            items = lessons.Select(x => new
            {
                x.Id,
                kind = x.Kind.ToString(),
                status = x.Status.ToString(),
                x.StartAtUtc,
                x.DurationMinutes,
                x.Notes,
                x.StudentConfirmedAtUtc,
                x.StudentConfirmationNote,
                instructorName = x.Instructor!.FullName,
                courseName = x.Enrollment?.Course?.Name,
                sessionTitle = ResolveSessionTitle(x),
                timelineLabel = ResolveTimelineLabel(x),
                evolutionSummary = ResolveEvolutionSummary(x),
                statusMessage = ResolveHistoryStatusMessage(x)
            })
        });
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var access = await ResolveStudentAccessAsync();
        if (access.Error is not null)
        {
            return access.Error;
        }

        var student = access.Student!;
        var now = DateTime.UtcNow;
        var lessonScope = _dbContext.Lessons.Where(x => x.SchoolId == access.SchoolId && x.StudentId == student.Id);
        var realizedLessons = await lessonScope.CountAsync(x => x.Status == LessonStatus.Realized);
        var upcomingLessons = await lessonScope.CountAsync(x =>
            x.StartAtUtc >= now &&
            (x.Status == LessonStatus.Scheduled || x.Status == LessonStatus.Confirmed));

        return Ok(new
        {
            student = new
            {
                student.Id,
                student.FullName,
                student.Email,
                student.Phone,
                student.BirthDate,
                student.MedicalNotes,
                student.EmergencyContactName,
                student.EmergencyContactPhone,
                student.FirstStandUpAtUtc,
                student.CreatedAtUtc
            },
            summary = new
            {
                profileCompleteness = CalculateProfileCompleteness(student),
                realizedLessons,
                upcomingLessons
            }
        });
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateStudentPortalProfileRequest request)
    {
        var access = await ResolveStudentAccessAsync();
        if (access.Error is not null)
        {
            return access.Error;
        }

        var student = access.Student!;
        var fullName = NormalizeNullable(request.FullName);
        if (fullName is null)
        {
            return BadRequest("Informe o nome completo do aluno.");
        }

        student.FullName = fullName;
        student.Phone = NormalizeNullable(request.Phone);
        student.BirthDate = request.BirthDate;
        student.MedicalNotes = NormalizeNullable(request.MedicalNotes);
        student.EmergencyContactName = NormalizeNullable(request.EmergencyContactName);
        student.EmergencyContactPhone = NormalizeNullable(request.EmergencyContactPhone);

        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            updatedAtUtc = DateTime.UtcNow,
            profileCompleteness = CalculateProfileCompleteness(student)
        });
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications()
    {
        var access = await ResolveStudentAccessAsync();
        if (access.Error is not null)
        {
            return access.Error;
        }

        var settings = await GetPortalSettingsAsync();
        var upcomingLessons = await _dbContext.Lessons
            .Where(x =>
                x.SchoolId == access.SchoolId &&
                x.StudentId == access.Student!.Id &&
                x.StartAtUtc >= DateTime.UtcNow &&
                (x.Status == LessonStatus.Scheduled || x.Status == LessonStatus.Confirmed))
            .Include(x => x.Enrollment)
            .ThenInclude(x => x!.Course)
            .OrderBy(x => x.StartAtUtc)
            .Take(8)
            .Select(x => new
            {
                x.Id,
                kind = x.Kind.ToString(),
                status = x.Status.ToString(),
                x.StartAtUtc,
                x.DurationMinutes,
                x.Notes,
                x.StudentConfirmedAtUtc,
                x.StudentConfirmationNote,
                instructor = new
                {
                    instructorId = x.InstructorId,
                    name = string.Empty
                },
                enrollment = x.EnrollmentId.HasValue && x.Enrollment != null
                    ? new
                    {
                        enrollmentId = x.EnrollmentId,
                        x.Enrollment.CourseId,
                        courseName = x.Enrollment.Course!.Name
                    }
                    : null
            })
            .ToListAsync();

        var notifications = await LoadPortalNotificationsAsync(access.Student!.Id, settings, upcomingLessons.Cast<object>().ToList());
        return Ok(new
        {
            unreadCount = notifications.Count(x => x.ReadAtUtc is null),
            items = notifications
        });
    }

    [HttpPost("notifications/read-all")]
    public async Task<IActionResult> ReadAllNotifications()
    {
        var access = await ResolveStudentAccessAsync();
        if (access.Error is not null)
        {
            return access.Error;
        }

        var now = DateTime.UtcNow;
        var unreadItems = await _dbContext.StudentPortalNotifications
            .Where(x =>
                x.SchoolId == access.SchoolId &&
                x.StudentId == access.Student!.Id &&
                x.ReadAtUtc == null)
            .ToListAsync();

        foreach (var item in unreadItems)
        {
            item.ReadAtUtc = now;
        }

        await _dbContext.SaveChangesAsync();
        return Ok(new { readCount = unreadItems.Count, readAtUtc = now });
    }

    [HttpPost("notifications/{notificationId:guid}/read")]
    public async Task<IActionResult> ReadNotification(Guid notificationId)
    {
        var access = await ResolveStudentAccessAsync();
        if (access.Error is not null)
        {
            return access.Error;
        }

        var notification = await _dbContext.StudentPortalNotifications.FirstOrDefaultAsync(x =>
            x.Id == notificationId &&
            x.SchoolId == access.SchoolId &&
            x.StudentId == access.Student!.Id);

        if (notification is null)
        {
            return NotFound("A notificação informada não foi encontrada.");
        }

        notification.ReadAtUtc ??= DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return Ok(new { notification.Id, notification.ReadAtUtc });
    }

    [HttpPost("lessons/course")]
    public async Task<IActionResult> ScheduleCourseLesson([FromBody] ScheduleCourseLessonRequest request)
    {
        var access = await ResolveStudentAccessAsync();
        if (access.Error is not null)
        {
            return access.Error;
        }

        var schoolId = access.SchoolId;
        var student = access.Student!;
        var settings = await GetPortalSettingsAsync();

        if (!await EnsureStudentFinanciallyEligibleAsync(schoolId, student.Id))
        {
            return BadRequest("Existem cobranças em atraso para este aluno. Regularize a situação financeira antes de agendar uma nova aula pelo portal.");
        }

        if (request.DurationMinutes <= 0)
        {
            return BadRequest("A duração da aula precisa ser maior que zero.");
        }

        if (!CanBookLesson(request.StartAtUtc, settings))
        {
            return BadRequest($"As aulas precisam ser agendadas com pelo menos {settings.BookingLeadTimeMinutes} minutos de antecedência.");
        }

        var enrollment = await _dbContext.Enrollments
            .Include(x => x.Course)
            .FirstOrDefaultAsync(x =>
                x.Id == request.EnrollmentId &&
                x.SchoolId == schoolId &&
                x.StudentId == student.Id);

        if (enrollment is null)
        {
            return BadRequest("A matrícula informada não pertence ao aluno logado.");
        }

        if (enrollment.Status != EnrollmentStatus.Active)
        {
            return BadRequest("Somente matrículas ativas podem receber novos agendamentos.");
        }

        var instructorExists = await _dbContext.Instructors.AnyAsync(x =>
            x.Id == request.InstructorId &&
            x.SchoolId == schoolId &&
            x.IsActive);

        if (!instructorExists)
        {
            return BadRequest("O instrutor selecionado não está disponível para a escola atual.");
        }

        var availableToScheduleMinutes = await CalculateAvailableToScheduleAsync(schoolId, enrollment);
        if (availableToScheduleMinutes < request.DurationMinutes)
        {
            return BadRequest("Essa matrícula não possui saldo horário livre suficiente para esse agendamento.");
        }

        var lesson = new Lesson
        {
            SchoolId = schoolId,
            StudentId = student.Id,
            InstructorId = request.InstructorId,
            Kind = LessonKind.Course,
            Status = LessonStatus.Scheduled,
            EnrollmentId = enrollment.Id,
            StartAtUtc = request.StartAtUtc,
            DurationMinutes = request.DurationMinutes,
            Notes = NormalizeNullable(request.Notes)
        };

        var schedulingValidation = await _lessonSchedulingService.ValidateAsync(schoolId, lesson);
        if (!schedulingValidation.IsValid)
        {
            return Conflict(schedulingValidation.ErrorMessage);
        }

        _dbContext.Lessons.Add(lesson);

        await CreateNotificationAsync(
            settings,
            student.Id,
            lesson.Id,
            "LessonScheduled",
            "Nova aula agendada",
            $"Sua aula de {enrollment.Course!.Name} foi colocada na agenda para {FormatUtcForMessage(request.StartAtUtc)}.",
            "Ver agenda",
            "/student");

        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            lessonId = lesson.Id,
            lesson.StartAtUtc,
            lesson.DurationMinutes
        });
    }

    [HttpPost("lessons/{lessonId:guid}/reschedule")]
    public async Task<IActionResult> RescheduleLesson(Guid lessonId, [FromBody] RescheduleStudentLessonRequest request)
    {
        var access = await ResolveStudentAccessAsync();
        if (access.Error is not null)
        {
            return access.Error;
        }

        var schoolId = access.SchoolId;
        var student = access.Student!;
        var settings = await GetPortalSettingsAsync();
        var now = DateTime.UtcNow;

        if (!await EnsureStudentFinanciallyEligibleAsync(schoolId, student.Id))
        {
            return BadRequest("Existem cobranças em atraso para este aluno. Regularize a situação financeira antes de remarcar uma aula pelo portal.");
        }

        if (request.DurationMinutes <= 0)
        {
            return BadRequest("A duração da aula precisa ser maior que zero.");
        }

        if (!CanBookLesson(request.StartAtUtc, settings))
        {
            return BadRequest($"A nova data precisa respeitar a antecedência mínima de {settings.BookingLeadTimeMinutes} minutos.");
        }

        var lesson = await _dbContext.Lessons
            .Include(x => x.Enrollment)
            .ThenInclude(x => x!.Course)
            .FirstOrDefaultAsync(x =>
                x.Id == lessonId &&
                x.SchoolId == schoolId &&
                x.StudentId == student.Id);

        if (lesson is null)
        {
            return NotFound("A aula informada não foi encontrada para este aluno.");
        }

        if (lesson.Kind != LessonKind.Course || !lesson.EnrollmentId.HasValue)
        {
            return BadRequest("Apenas aulas de curso podem ser remarcadas pelo portal do aluno.");
        }

        if (lesson.Status is not (LessonStatus.Scheduled or LessonStatus.Confirmed))
        {
            return BadRequest("Somente aulas futuras e abertas podem ser remarcadas.");
        }

        if (!CanRescheduleLesson(lesson, now, settings))
        {
            return BadRequest($"Essa aula não pode mais ser remarcada pelo portal. A escola exige no mínimo {settings.RescheduleWindowHours} horas de antecedência.");
        }

        var instructorExists = await _dbContext.Instructors.AnyAsync(x =>
            x.Id == request.InstructorId &&
            x.SchoolId == schoolId &&
            x.IsActive);

        if (!instructorExists)
        {
            return BadRequest("O instrutor selecionado não está disponível para a escola atual.");
        }

        var enrollment = await _dbContext.Enrollments.FirstOrDefaultAsync(x =>
            x.Id == lesson.EnrollmentId!.Value &&
            x.SchoolId == schoolId &&
            x.StudentId == student.Id);

        if (enrollment is null)
        {
            return BadRequest("A matrícula vinculada à aula não foi encontrada para este aluno.");
        }

        var availableToScheduleMinutes = await CalculateAvailableToScheduleAsync(
            schoolId,
            enrollment,
            lesson.Id);

        if (availableToScheduleMinutes < request.DurationMinutes)
        {
            return BadRequest("A matrícula não possui saldo horário suficiente para remarcar com essa duração.");
        }

        lesson.Status = LessonStatus.Rescheduled;
        lesson.Notes = AppendPortalNote(lesson.Notes, request.Reason, "Aula remarcada pelo aluno no portal.");

        var replacement = new Lesson
        {
            SchoolId = schoolId,
            StudentId = lesson.StudentId,
            InstructorId = request.InstructorId,
            Kind = lesson.Kind,
            Status = LessonStatus.Scheduled,
            EnrollmentId = lesson.EnrollmentId,
            StartAtUtc = request.StartAtUtc,
            DurationMinutes = request.DurationMinutes,
            Notes = NormalizeNullable(request.Notes)
        };

        var schedulingValidation = await _lessonSchedulingService.ValidateAsync(schoolId, replacement, lesson.Id);
        if (!schedulingValidation.IsValid)
        {
            return Conflict(schedulingValidation.ErrorMessage);
        }

        _dbContext.Lessons.Add(replacement);

        var courseName = lesson.Enrollment?.Course?.Name ?? "sua aula";
        await CreateNotificationAsync(
            settings,
            student.Id,
            replacement.Id,
            "LessonRescheduled",
            "Aula remarcada com sucesso",
            $"Sua aula de {courseName} foi remarcada para {FormatUtcForMessage(request.StartAtUtc)}.",
            "Ver nova aula",
            "/student");

        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            previousLessonId = lesson.Id,
            newLessonId = replacement.Id
        });
    }

    [HttpPost("lessons/{lessonId:guid}/cancel")]
    public async Task<IActionResult> CancelLesson(Guid lessonId, [FromBody] CancelStudentLessonRequest request)
    {
        var access = await ResolveStudentAccessAsync();
        if (access.Error is not null)
        {
            return access.Error;
        }

        var schoolId = access.SchoolId;
        var student = access.Student!;
        var settings = await GetPortalSettingsAsync();
        var now = DateTime.UtcNow;

        var lesson = await _dbContext.Lessons
            .Include(x => x.Enrollment)
            .ThenInclude(x => x!.Course)
            .FirstOrDefaultAsync(x =>
                x.Id == lessonId &&
                x.SchoolId == schoolId &&
                x.StudentId == student.Id);

        if (lesson is null)
        {
            return NotFound("A aula informada não foi encontrada para este aluno.");
        }

        if (lesson.Kind != LessonKind.Course || !lesson.EnrollmentId.HasValue)
        {
            return BadRequest("Apenas aulas de curso podem ser canceladas pelo portal do aluno.");
        }

        if (lesson.Status is not (LessonStatus.Scheduled or LessonStatus.Confirmed))
        {
            return BadRequest("Somente aulas futuras e abertas podem ser canceladas.");
        }

        if (!CanCancelLesson(lesson, now, settings))
        {
            return BadRequest($"Essa aula não pode mais ser cancelada pelo portal. A escola exige no mínimo {settings.CancellationWindowHours} horas de antecedência.");
        }

        lesson.Status = LessonStatus.Cancelled;
        lesson.Notes = AppendPortalNote(lesson.Notes, request.Reason, "Aula cancelada pelo aluno no portal.");

        var courseName = lesson.Enrollment?.Course?.Name ?? "sua aula";
        await CreateNotificationAsync(
            settings,
            student.Id,
            lesson.Id,
            "LessonCancelled",
            "Aula cancelada",
            $"O cancelamento da aula de {courseName} para {FormatUtcForMessage(lesson.StartAtUtc)} foi registrado no portal.",
            "Ver histórico",
            "/student/history");

        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            lessonId = lesson.Id,
            status = lesson.Status.ToString()
        });
    }

    [HttpPost("lessons/{lessonId:guid}/confirm-presence")]
    public async Task<IActionResult> ConfirmPresence(Guid lessonId, [FromBody] ConfirmPresenceRequest request)
    {
        var access = await ResolveStudentAccessAsync();
        if (access.Error is not null)
        {
            return access.Error;
        }

        var schoolId = access.SchoolId;
        var student = access.Student!;
        var settings = await GetPortalSettingsAsync();
        var now = DateTime.UtcNow;

        var lesson = await _dbContext.Lessons
            .Include(x => x.Enrollment)
            .ThenInclude(x => x!.Course)
            .FirstOrDefaultAsync(x =>
                x.Id == lessonId &&
                x.SchoolId == schoolId &&
                x.StudentId == student.Id);

        if (lesson is null)
        {
            return NotFound("A aula informada não foi encontrada para este aluno.");
        }

        if (lesson.Status is not (LessonStatus.Scheduled or LessonStatus.Confirmed))
        {
            return BadRequest("Somente aulas futuras ainda abertas podem receber confirmação de presença.");
        }

        if (!CanConfirmPresence(lesson, now, settings))
        {
            return BadRequest($"A presença só pode ser confirmada a partir de {settings.AttendanceConfirmationLeadMinutes} minutos antes do início da aula.");
        }

        lesson.StudentConfirmedAtUtc = now;
        lesson.StudentConfirmationNote = NormalizeNullable(request.Note);
        if (lesson.Status == LessonStatus.Scheduled)
        {
            lesson.Status = LessonStatus.Confirmed;
        }

        var courseName = lesson.Enrollment?.Course?.Name ?? "sua aula";
        await CreateNotificationAsync(
            settings,
            student.Id,
            lesson.Id,
            "PresenceConfirmed",
            "Presença confirmada",
            $"Sua confirmação de presença para {courseName} em {FormatUtcForMessage(lesson.StartAtUtc)} foi registrada.",
            "Ver agenda",
            "/student");

        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            lessonId = lesson.Id,
            lesson.StudentConfirmedAtUtc,
            lesson.StudentConfirmationNote,
            status = lesson.Status.ToString()
        });
    }

    private async Task<StudentAccessResolution> ResolveStudentAccessAsync()
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var role = User.FindFirstValue(ClaimTypes.Role);
        if (!string.Equals(role, "Student", StringComparison.Ordinal))
        {
            return StudentAccessResolution.Forbid(Forbid());
        }

        var identityUserIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(identityUserIdRaw, out var identityUserId) && identityUserId != Guid.Empty)
        {
            var studentByIdentity = await _dbContext.Students.FirstOrDefaultAsync(x =>
                x.SchoolId == schoolId &&
                x.IsActive &&
                x.IdentityUserId == identityUserId);

            if (studentByIdentity is not null)
            {
                return StudentAccessResolution.Success(schoolId, studentByIdentity);
            }
        }

        var email = User.FindFirstValue(ClaimTypes.Email)?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            return StudentAccessResolution.Unauthorized(Unauthorized());
        }

        var student = await _dbContext.Students.FirstOrDefaultAsync(x =>
            x.SchoolId == schoolId &&
            x.IsActive &&
            x.Email != null &&
            EF.Functions.ILike(x.Email, email));

        if (student is null)
        {
            return StudentAccessResolution.NotFound(NotFound("Não encontramos um aluno ativo vinculado ao seu acesso do portal."));
        }

        return StudentAccessResolution.Success(schoolId, student);
    }

    private async Task<PortalSchoolSettings> GetPortalSettingsAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("schools");
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/schools/portal-settings");

            if (Request.Headers.TryGetValue("Authorization", out var authorization))
            {
                request.Headers.TryAddWithoutValidation("Authorization", authorization.ToString());
            }

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                return DefaultPortalSettings;
            }

            var payload = await response.Content.ReadFromJsonAsync<PortalSchoolSettings>();
            return payload ?? DefaultPortalSettings;
        }
        catch
        {
            return DefaultPortalSettings;
        }
    }

    private async Task<bool> EnsureStudentFinanciallyEligibleAsync(Guid schoolId, Guid studentId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("finance");
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"api/v1/finance/internal/students/financial-statuses?schoolId={schoolId}&studentId={studentId}");

            var sharedKey = _configuration["InternalServiceAuth:SharedKey"];
            if (string.IsNullOrWhiteSpace(sharedKey))
            {
                return false;
            }

            request.Headers.TryAddWithoutValidation("X-KiteFlow-Internal-Key", sharedKey);

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var payload = await response.Content.ReadFromJsonAsync<StudentFinancialStatusesEnvelope>();
            var status = payload?.Items.FirstOrDefault()?.Status;
            return !string.Equals(status, "Delinquent", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private async Task<int> CalculateAvailableToScheduleAsync(Guid schoolId, Enrollment enrollment, Guid? ignoredLessonId = null)
    {
        var scheduledMinutes = await _dbContext.Lessons
            .Where(x =>
                x.SchoolId == schoolId &&
                x.EnrollmentId == enrollment.Id &&
                x.StartAtUtc >= DateTime.UtcNow &&
                (x.Status == LessonStatus.Scheduled || x.Status == LessonStatus.Confirmed) &&
                (!ignoredLessonId.HasValue || x.Id != ignoredLessonId.Value))
            .SumAsync(x => (int?)x.DurationMinutes) ?? 0;

        var remainingMinutes = Math.Max(0, enrollment.IncludedMinutesSnapshot - enrollment.UsedMinutes);
        return Math.Max(0, remainingMinutes - scheduledMinutes);
    }

    private async Task<bool> HasScheduleConflictAsync(
        Guid schoolId,
        Guid studentId,
        Guid instructorId,
        DateTime startAtUtc,
        int durationMinutes,
        Guid? ignoredLessonId = null)
    {
        var candidateEnd = startAtUtc.AddMinutes(durationMinutes);
        var windowStart = startAtUtc.AddHours(-6);
        var windowEnd = candidateEnd.AddHours(6);

        var nearbyLessons = await _dbContext.Lessons
            .Where(x =>
                x.SchoolId == schoolId &&
                (x.StudentId == studentId || x.InstructorId == instructorId) &&
                x.StartAtUtc >= windowStart &&
                x.StartAtUtc <= windowEnd &&
                BlockingStatuses.Contains(x.Status) &&
                (!ignoredLessonId.HasValue || x.Id != ignoredLessonId.Value))
            .Select(x => new
            {
                x.StartAtUtc,
                x.DurationMinutes
            })
            .ToListAsync();

        return nearbyLessons.Any(x =>
        {
            var existingEnd = x.StartAtUtc.AddMinutes(x.DurationMinutes);
            return x.StartAtUtc < candidateEnd && startAtUtc < existingEnd;
        });
    }

    private async Task<List<PortalNotificationView>> LoadPortalNotificationsAsync(
        Guid studentId,
        PortalSchoolSettings settings,
        IReadOnlyList<object> upcomingLessons)
    {
        if (!settings.PortalNotificationsEnabled)
        {
            return [];
        }

        var stored = await _dbContext.StudentPortalNotifications
            .Where(x =>
                x.SchoolId == _currentTenant.SchoolId!.Value &&
                x.StudentId == studentId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(30)
            .Select(x => new PortalNotificationView(
                x.Id,
                x.Category,
                x.Title,
                x.Message,
                x.ActionLabel,
                x.ActionPath,
                x.CreatedAtUtc,
                x.ReadAtUtc,
                false))
            .ToListAsync();

        var reminderItems = BuildReminderNotifications(upcomingLessons, settings);
        return stored
            .Concat(reminderItems)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToList();
    }

    private static List<PortalNotificationView> BuildReminderNotifications(
        IReadOnlyList<object> upcomingLessons,
        PortalSchoolSettings settings)
    {
        if (!settings.PortalNotificationsEnabled)
        {
            return [];
        }

        var now = DateTime.UtcNow;
        var reminderLimit = now.AddHours(settings.LessonReminderLeadHours);
        var syntheticIdSeed = 0;
        var items = new List<PortalNotificationView>();

        foreach (var lessonObject in upcomingLessons)
        {
            dynamic lesson = lessonObject;
            DateTime startAtUtc = lesson.StartAtUtc;
            if (startAtUtc <= now || startAtUtc > reminderLimit)
            {
                continue;
            }

            string courseName = lesson.enrollment?.courseName ?? "sua aula";
            items.Add(new PortalNotificationView(
                GuidUtility.CreateDeterministic(GuidUtility.UrlNamespace, $"portal-reminder-{++syntheticIdSeed}-{startAtUtc:o}"),
                "LessonReminder",
                "Aula se aproximando",
                $"Sua aula de {courseName} começa em {Math.Max(0, (int)Math.Round((startAtUtc - now).TotalMinutes))} minutos. Confirme sua presença no portal se já estiver tudo certo.",
                "Abrir agenda",
                "/student",
                startAtUtc.AddMinutes(-settings.AttendanceConfirmationLeadMinutes),
                null,
                true));
        }

        return items;
    }

    private async Task CreateNotificationAsync(
        PortalSchoolSettings settings,
        Guid studentId,
        Guid? lessonId,
        string category,
        string title,
        string message,
        string? actionLabel,
        string? actionPath)
    {
        if (!settings.PortalNotificationsEnabled)
        {
            return;
        }

        _dbContext.StudentPortalNotifications.Add(new StudentPortalNotification
        {
            SchoolId = _currentTenant.SchoolId!.Value,
            StudentId = studentId,
            LessonId = lessonId,
            Category = category,
            Title = title,
            Message = message,
            ActionLabel = actionLabel,
            ActionPath = actionPath
        });

        await Task.CompletedTask;
    }

    private static object BuildTrainingProgress(
        Student student,
        IReadOnlyList<Lesson> realizedLessons,
        IReadOnlyList<Enrollment> enrollments,
        int completedCourses)
    {
        var totalRealizedLessons = realizedLessons.Count;
        var firstRealizedAtUtc = realizedLessons.FirstOrDefault()?.StartAtUtc;
        var lastRealizedAtUtc = realizedLessons.LastOrDefault()?.StartAtUtc;
        var consistentNavigationAtUtc = GetNthRealizedLessonDate(realizedLessons, 5);
        var autonomyAtUtc = GetNthRealizedLessonDate(realizedLessons, 12);
        var firstCompletedCourseAtUtc = enrollments
            .Where(x => x.Status == EnrollmentStatus.Completed)
            .OrderBy(x => x.EndedAtUtc ?? x.StartedAtUtc)
            .Select(x => (DateTime?)(x.EndedAtUtc ?? x.StartedAtUtc))
            .FirstOrDefault();

        var tracks = new[]
        {
            BuildTrack("wind-foundations", "Fundamentos de vento e segurança", "Leitura da condição, janela de vento, equipamento e preparação para entrar na água.", totalRealizedLessons >= 2 ? 100 : totalRealizedLessons * 50, GetNthRealizedLessonDate(realizedLessons, 2)),
            BuildTrack("waterstart", "Waterstart e primeiros bordos", "Primeiras saídas consistentes, controle de prancha e sustentação do waterstart.", student.FirstStandUpAtUtc.HasValue ? 100 : Math.Clamp(totalRealizedLessons * 22, 0, 92), student.FirstStandUpAtUtc),
            BuildTrack("consistent-riding", "Navegação consistente", "Ritmo de aula mais estável, transições e manutenção de bordo com menor intervenção.", totalRealizedLessons >= 8 ? 100 : Math.Clamp((totalRealizedLessons - 2) * 16, 0, 88), GetNthRealizedLessonDate(realizedLessons, 8)),
            BuildTrack("supervised-autonomy", "Autonomia supervisionada", "Planejamento de sessão, leitura de risco e execução com supervisão mais leve do instrutor.", completedCourses > 0 || totalRealizedLessons >= 14 ? 100 : Math.Clamp((totalRealizedLessons - 7) * 14, 0, 86), firstCompletedCourseAtUtc ?? autonomyAtUtc)
        };

        var modules = new[]
        {
            BuildModule("setup", "Preparação e segurança", "Base de leitura de vento, montagem e protocolos essenciais antes de entrar na água.", totalRealizedLessons, new[]
            {
                BuildSkill("Janela de vento", totalRealizedLessons >= 1, totalRealizedLessons >= 2 ? 100 : 55),
                BuildSkill("Checagem de equipamento", totalRealizedLessons >= 1, totalRealizedLessons >= 2 ? 100 : 65),
                BuildSkill("Protocolos de segurança", totalRealizedLessons >= 2, totalRealizedLessons >= 3 ? 100 : 72)
            }),
            BuildModule("waterstart", "Waterstart e controle inicial", "Momento de ligar tecnica, sustentacao e repeticao de saidas para ganhar confianca.", totalRealizedLessons, new[]
            {
                BuildSkill("Posicionamento do corpo", totalRealizedLessons >= 2, Math.Clamp(totalRealizedLessons * 18, 0, 100)),
                BuildSkill("Saída da água", student.FirstStandUpAtUtc.HasValue, student.FirstStandUpAtUtc.HasValue ? 100 : Math.Clamp(totalRealizedLessons * 20, 0, 90)),
                BuildSkill("Primeiros bordos", totalRealizedLessons >= 4, totalRealizedLessons >= 6 ? 100 : Math.Clamp((totalRealizedLessons - 2) * 20, 0, 92))
            }),
            BuildModule("consistency", "Consistência de navegação", "Foco em repetição, transições e manutenção de ritmo com menos intervenção.", totalRealizedLessons, new[]
            {
                BuildSkill("Controle de borda", totalRealizedLessons >= 5, Math.Clamp((totalRealizedLessons - 3) * 16, 0, 100)),
                BuildSkill("Transicoes basicas", totalRealizedLessons >= 7, Math.Clamp((totalRealizedLessons - 4) * 14, 0, 100)),
                BuildSkill("Leitura de condicao", totalRealizedLessons >= 8, Math.Clamp((totalRealizedLessons - 5) * 14, 0, 100))
            }),
            BuildModule("autonomy", "Autonomia supervisionada", "Etapa em que a rotina de treino fica mais madura, com mais leitura própria da sessão.", totalRealizedLessons, new[]
            {
                BuildSkill("Planejamento de sessão", totalRealizedLessons >= 10, Math.Clamp((totalRealizedLessons - 8) * 14, 0, 100)),
                BuildSkill("Gestao de risco", totalRealizedLessons >= 12, Math.Clamp((totalRealizedLessons - 9) * 13, 0, 100)),
                BuildSkill("Ritmo de autonomia", completedCourses > 0 || totalRealizedLessons >= 14, completedCourses > 0 || totalRealizedLessons >= 14 ? 100 : Math.Clamp((totalRealizedLessons - 10) * 12, 0, 94))
            })
        };

        var readinessScore = (int)Math.Round(tracks.Average(x => x.ProgressPercent));
        var nextTrack = tracks.FirstOrDefault(x => x.Status != "Completed");

        return new
        {
            readinessScore,
            currentFocus = nextTrack?.Title ?? "Treino de refinamento e autonomia",
            recommendedNextStep = ResolveRecommendedNextStep(student, totalRealizedLessons, completedCourses, nextTrack?.Title),
            lastTrainingAtUtc = lastRealizedAtUtc,
            tracks,
            modules,
            milestones = new[]
            {
                BuildMilestone("Primeira aula realizada", "Marca o início efetivo da trilha prática no portal.", firstRealizedAtUtc),
                BuildMilestone("Primeiro stand-up registrado", "Indica a transição do aluno para uma fase de maior estabilidade na água.", student.FirstStandUpAtUtc),
                BuildMilestone("Sequência de navegação consistente", "Considera quando o aluno sustenta uma sequência mínima de aulas realizadas.", consistentNavigationAtUtc),
                BuildMilestone("Primeiro curso concluído", "Mostra a conclusão de um ciclo completo de aprendizagem.", firstCompletedCourseAtUtc)
            }
        };
    }

    private static TrainingTrack BuildTrack(
        string id,
        string title,
        string description,
        int progressPercent,
        DateTime? achievedAtUtc)
    {
        var normalizedProgress = Math.Clamp(progressPercent, 0, 100);
        var status = normalizedProgress >= 100
            ? "Completed"
            : normalizedProgress > 0
                ? "InProgress"
                : "Locked";

        return new TrainingTrack(id, title, description, status, normalizedProgress, achievedAtUtc);
    }

    private static TrainingModule BuildModule(
        string id,
        string title,
        string description,
        int totalRealizedLessons,
        IReadOnlyList<TrainingSkill> skills)
    {
        var progressPercent = skills.Count == 0 ? 0 : (int)Math.Round(skills.Average(x => x.ProgressPercent));
        var status = skills.All(x => x.Status == "Completed")
            ? "Completed"
            : totalRealizedLessons == 0
                ? "Locked"
                : "InProgress";

        return new TrainingModule(id, title, description, status, progressPercent, skills);
    }

    private static TrainingSkill BuildSkill(string title, bool completed, int progressPercent)
        => new(
            title,
            completed ? "Completed" : progressPercent > 0 ? "InProgress" : "Locked",
            Math.Clamp(progressPercent, 0, 100));

    private static TrainingMilestone BuildMilestone(string title, string description, DateTime? occurredAtUtc)
        => new(title, description, occurredAtUtc.HasValue ? "Completed" : "Pending", occurredAtUtc);

    private static DateTime? GetNthRealizedLessonDate(IReadOnlyList<Lesson> realizedLessons, int position)
    {
        if (position <= 0 || realizedLessons.Count < position)
        {
            return null;
        }

        return realizedLessons[position - 1].StartAtUtc;
    }

    private static string ResolveRecommendedNextStep(
        Student student,
        int totalRealizedLessons,
        int completedCourses,
        string? nextTrackTitle)
    {
        if (completedCourses > 0 || totalRealizedLessons >= 14)
        {
            return "Seu momento pede sessões com foco em refinamento técnico, leitura de condição e consistência de autonomia supervisionada.";
        }

        if (!student.FirstStandUpAtUtc.HasValue)
        {
            return "Priorize aulas que reforcem waterstart, posicionamento de prancha e saídas mais repetíveis na água.";
        }

        if (totalRealizedLessons < 8)
        {
            return "Vale focar em transições, controle de borda e repetição de manobras básicas para ganhar fluidez.";
        }

        return $"Seu próximo salto está em {nextTrackTitle ?? "navegação consistente"}, com prática frequente e menos intervalo entre aulas.";
    }

    private static string ResolveTrainingStage(DateTime? firstStandUpAtUtc, int totalRealizedLessons)
    {
        if (firstStandUpAtUtc.HasValue)
        {
            return "Primeiros bordos consolidados";
        }

        if (totalRealizedLessons >= 12)
        {
            return "Aluno em fase de autonomia";
        }

        if (totalRealizedLessons >= 5)
        {
            return "Transição para navegação consistente";
        }

        return "Fundamentos e adaptação ao vento";
    }

    private static bool CanBookLesson(DateTime startAtUtc, PortalSchoolSettings settings)
        => startAtUtc >= DateTime.UtcNow.AddMinutes(settings.BookingLeadTimeMinutes);

    private static bool CanCancelLesson(Lesson lesson, DateTime now, PortalSchoolSettings settings)
        => lesson.StartAtUtc >= now.AddHours(settings.CancellationWindowHours);

    private static bool CanRescheduleLesson(Lesson lesson, DateTime now, PortalSchoolSettings settings)
        => lesson.StartAtUtc >= now.AddHours(settings.RescheduleWindowHours);

    private static bool CanConfirmPresence(Lesson lesson, DateTime now, PortalSchoolSettings settings)
    {
        var confirmationStart = lesson.StartAtUtc.AddMinutes(-settings.AttendanceConfirmationLeadMinutes);
        var confirmationEnd = lesson.StartAtUtc.AddMinutes(lesson.DurationMinutes);
        return now >= confirmationStart && now <= confirmationEnd;
    }

    private static int CalculateProfileCompleteness(Student student)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(student.FullName)) score += 18;
        if (!string.IsNullOrWhiteSpace(student.Email)) score += 18;
        if (!string.IsNullOrWhiteSpace(student.Phone)) score += 14;
        if (student.BirthDate.HasValue) score += 12;
        if (!string.IsNullOrWhiteSpace(student.EmergencyContactName)) score += 12;
        if (!string.IsNullOrWhiteSpace(student.EmergencyContactPhone)) score += 12;
        if (!string.IsNullOrWhiteSpace(student.MedicalNotes)) score += 7;
        if (student.FirstStandUpAtUtc.HasValue) score += 7;
        return Math.Clamp(score, 0, 100);
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string AppendPortalNote(string? currentNotes, string? providedReason, string fallback)
    {
        var reason = NormalizeNullable(providedReason) ?? fallback;
        if (string.IsNullOrWhiteSpace(currentNotes))
        {
            return reason;
        }

        return $"{currentNotes.Trim()} | {reason}";
    }

    private static string ResolveSessionTitle(Lesson lesson)
        => lesson.Kind == LessonKind.Course
            ? $"Sessão de curso {lesson.Enrollment?.Course?.Name ?? string.Empty}".Trim()
            : "Aula avulsa";

    private static string ResolveTimelineLabel(Lesson lesson)
        => lesson.Status switch
        {
            LessonStatus.Realized => "Aula concluída",
            LessonStatus.Confirmed => "Presença confirmada",
            LessonStatus.Cancelled => "Aula cancelada",
            LessonStatus.Rescheduled => "Aula remarcada",
            LessonStatus.NoShow => "Ausencia registrada",
            _ => "Aula planejada"
        };

    private static string ResolveEvolutionSummary(Lesson lesson)
    {
        if (!string.IsNullOrWhiteSpace(lesson.Notes))
        {
            return lesson.Notes!;
        }

        if (lesson.Status == LessonStatus.Realized)
        {
            return "Sessão registrada como realizada. Use a linha do tempo para acompanhar seu ritmo de treinamento.";
        }

        if (lesson.Status == LessonStatus.Cancelled)
        {
            return "A aula saiu da agenda e não consumirá carga horária do curso.";
        }

        if (lesson.Status == LessonStatus.Rescheduled)
        {
            return "A aula original foi preservada no histórico como referência de remarcação.";
        }

        return "Sessão registrada no portal do aluno.";
    }

    private static string ResolveHistoryStatusMessage(Lesson lesson)
    {
        if (lesson.StudentConfirmedAtUtc.HasValue)
        {
            return $"Presença confirmada pelo aluno em {FormatUtcForMessage(lesson.StudentConfirmedAtUtc.Value)}.";
        }

        return lesson.Status switch
        {
            LessonStatus.Realized => "Treino contabilizado no histórico e no progresso do curso.",
            LessonStatus.Cancelled => "Cancelamento registrado sem consumo de saldo horário.",
            LessonStatus.Rescheduled => "A aula foi remarcada e a nova sessão aparece na agenda atual.",
            LessonStatus.Confirmed => "A aula está confirmada e pronta para acontecer.",
            _ => "Sessão disponível na trilha do aluno."
        };
    }

    private static string FormatUtcForMessage(DateTime value)
        => value.ToLocalTime().ToString("dd/MM/yyyy 'as' HH:mm");

    private sealed record StudentAccessResolution(Guid SchoolId, Student? Student, IActionResult? Error)
    {
        public static StudentAccessResolution Success(Guid schoolId, Student student) => new(schoolId, student, null);

        public static StudentAccessResolution Unauthorized(IActionResult error) => new(Guid.Empty, null, error);

        public static StudentAccessResolution Forbid(IActionResult error) => new(Guid.Empty, null, error);

        public static StudentAccessResolution NotFound(IActionResult error) => new(Guid.Empty, null, error);
    }

    private sealed record TrainingTrack(
        string Id,
        string Title,
        string Description,
        string Status,
        int ProgressPercent,
        DateTime? AchievedAtUtc);

    private sealed record TrainingModule(
        string Id,
        string Title,
        string Description,
        string Status,
        int ProgressPercent,
        IReadOnlyList<TrainingSkill> Skills);

    private sealed record TrainingSkill(
        string Title,
        string Status,
        int ProgressPercent);

    private sealed record TrainingMilestone(
        string Title,
        string Description,
        string Status,
        DateTime? OccurredAtUtc);

    private sealed record PortalSchoolSettings(
        int BookingLeadTimeMinutes,
        int CancellationWindowHours,
        int RescheduleWindowHours,
        int AttendanceConfirmationLeadMinutes,
        int LessonReminderLeadHours,
        bool PortalNotificationsEnabled,
        string ThemePrimary,
        string ThemeAccent);

    private sealed record PortalNotificationView(
        Guid Id,
        string Category,
        string Title,
        string Message,
        string? ActionLabel,
        string? ActionPath,
        DateTime CreatedAtUtc,
        DateTime? ReadAtUtc,
        bool IsSynthetic);

    private sealed record StudentFinancialStatusesEnvelope(IReadOnlyList<StudentFinancialStatusItem> Items);

    private sealed record StudentFinancialStatusItem(Guid StudentId, string Status);

    public sealed record ScheduleCourseLessonRequest(
        Guid EnrollmentId,
        Guid InstructorId,
        DateTime StartAtUtc,
        int DurationMinutes,
        string? Notes);

    public sealed record RescheduleStudentLessonRequest(
        Guid InstructorId,
        DateTime StartAtUtc,
        int DurationMinutes,
        string? Notes,
        string? Reason);

    public sealed record CancelStudentLessonRequest(string? Reason);

    public sealed record ConfirmPresenceRequest(string? Note);

    public sealed record UpdateStudentPortalProfileRequest(
        string? FullName,
        string? Phone,
        DateOnly? BirthDate,
        string? MedicalNotes,
        string? EmergencyContactName,
        string? EmergencyContactPhone);
}
