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
[Authorize(Policy = "LessonsAccess")]
[Route("api/v1/lessons")]
public sealed class LessonsController : ControllerBase
{
    private readonly AcademicsDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;
    private readonly LessonSchedulingService _lessonSchedulingService;
    private readonly SchoolOperationsSettingsClient _settingsClient;
    private readonly FinancialAutomationService _financialAutomationService;

    public LessonsController(
        AcademicsDbContext dbContext,
        ICurrentTenant currentTenant,
        LessonSchedulingService lessonSchedulingService,
        SchoolOperationsSettingsClient settingsClient,
        FinancialAutomationService financialAutomationService)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
        _lessonSchedulingService = lessonSchedulingService;
        _settingsClient = settingsClient;
        _financialAutomationService = financialAutomationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;
        var from = fromUtc ?? DateTime.UtcNow.AddDays(-14);
        var to = toUtc ?? DateTime.UtcNow.AddDays(30);

        var items = await _dbContext.Lessons
            .Where(x => x.SchoolId == schoolId && x.StartAtUtc >= from && x.StartAtUtc <= to)
            .Include(x => x.Student)
            .Include(x => x.Instructor)
            .Include(x => x.Enrollment)
            .OrderBy(x => x.StartAtUtc)
            .Select(x => new
            {
                x.Id,
                x.SchoolId,
                kind = x.Kind.ToString(),
                status = x.Status.ToString(),
                x.StudentId,
                studentName = x.Student!.FullName,
                x.InstructorId,
                instructorName = x.Instructor!.FullName,
                x.EnrollmentId,
                x.SingleLessonPrice,
                x.StartAtUtc,
                x.DurationMinutes,
                x.Notes,
                x.OperationalConfirmedAtUtc,
                x.OperationalConfirmedByUserId,
                x.OperationalConfirmationNote,
                x.NoShowMarkedAtUtc,
                x.NoShowMarkedByUserId,
                x.NoShowNote
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var lesson = await _dbContext.Lessons
            .Where(x => x.Id == id && x.SchoolId == schoolId)
            .Include(x => x.Student)
            .Include(x => x.Instructor)
            .Include(x => x.Enrollment)
            .Select(x => new
            {
                x.Id,
                x.SchoolId,
                kind = x.Kind.ToString(),
                status = x.Status.ToString(),
                x.StudentId,
                studentName = x.Student!.FullName,
                x.InstructorId,
                instructorName = x.Instructor!.FullName,
                x.EnrollmentId,
                x.SingleLessonPrice,
                x.StartAtUtc,
                x.DurationMinutes,
                x.Notes,
                x.OperationalConfirmedAtUtc,
                x.OperationalConfirmedByUserId,
                x.OperationalConfirmationNote,
                x.NoShowMarkedAtUtc,
                x.NoShowMarkedByUserId,
                x.NoShowNote
            })
            .FirstOrDefaultAsync();

        return lesson is null ? NotFound() : Ok(lesson);
    }

    [HttpGet("schedule-blocks")]
    public async Task<IActionResult> GetScheduleBlocks([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;
        var from = fromUtc ?? DateTime.UtcNow.AddDays(-7);
        var to = toUtc ?? DateTime.UtcNow.AddDays(30);

        var items = await _dbContext.ScheduleBlocks
            .Where(x => x.SchoolId == schoolId && x.StartAtUtc <= to && x.EndAtUtc >= from)
            .Include(x => x.Instructor)
            .OrderBy(x => x.StartAtUtc)
            .Select(x => new
            {
                x.Id,
                scope = x.Scope.ToString(),
                x.InstructorId,
                instructorName = x.Instructor != null ? x.Instructor.FullName : null,
                x.Title,
                x.Notes,
                x.StartAtUtc,
                x.EndAtUtc
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost("schedule-blocks")]
    public async Task<IActionResult> CreateScheduleBlock([FromBody] CreateScheduleBlockRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        if (request.EndAtUtc <= request.StartAtUtc)
        {
            return BadRequest("O bloqueio precisa terminar depois do horário de início.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest("O título do bloqueio é obrigatório.");
        }

        if (request.Scope == ScheduleBlockScope.Instructor)
        {
            var instructorExists = await _dbContext.Instructors.AnyAsync(x =>
                x.Id == request.InstructorId &&
                x.SchoolId == schoolId &&
                x.IsActive);

            if (!instructorExists)
            {
                return BadRequest("O instrutor do bloqueio não foi encontrado.");
            }
        }

        var block = new ScheduleBlock
        {
            SchoolId = schoolId,
            Scope = request.Scope,
            InstructorId = request.Scope == ScheduleBlockScope.Instructor ? request.InstructorId : null,
            Title = request.Title.Trim(),
            Notes = NormalizeNullable(request.Notes),
            StartAtUtc = request.StartAtUtc,
            EndAtUtc = request.EndAtUtc
        };

        _dbContext.ScheduleBlocks.Add(block);
        await _dbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(GetScheduleBlocks), new { id = block.Id }, new { blockId = block.Id });
    }

    [HttpDelete("schedule-blocks/{id:guid}")]
    public async Task<IActionResult> DeleteScheduleBlock(Guid id)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var block = await _dbContext.ScheduleBlocks.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
        if (block is null)
        {
            return NotFound();
        }

        _dbContext.ScheduleBlocks.Remove(block);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLessonRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var validation = await ValidateLessonReferences(request.StudentId, request.InstructorId, schoolId);
        if (validation is IActionResult error)
        {
            return error;
        }

        var lesson = new Lesson
        {
            SchoolId = schoolId,
            StudentId = request.StudentId,
            InstructorId = request.InstructorId,
            Kind = request.Kind,
            Status = request.Status,
            EnrollmentId = request.EnrollmentId,
            SingleLessonPrice = request.SingleLessonPrice,
            StartAtUtc = request.StartAtUtc,
            DurationMinutes = request.DurationMinutes,
            Notes = NormalizeNullable(request.Notes)
        };

        var lessonValidation = await ValidateLessonBusinessRulesAsync(lesson, schoolId);
        if (lessonValidation is IActionResult lessonError)
        {
            return lessonError;
        }

        var settings = await _settingsClient.GetAsync(schoolId);
        var schedulingValidation = await _lessonSchedulingService.ValidateAsync(schoolId, lesson);
        if (!schedulingValidation.IsValid)
        {
            return BadRequest(schedulingValidation.ErrorMessage);
        }

        _dbContext.Lessons.Add(lesson);

        var transitionResult = await ApplyEnrollmentTransitionAsync(
            schoolId,
            lesson,
            previousStatus: LessonStatus.Scheduled,
            nextStatus: lesson.Status,
            settings: settings);

        if (transitionResult is IActionResult transitionError)
        {
            return transitionError;
        }

        await _dbContext.SaveChangesAsync();
        await SyncLessonRevenueAsync(lesson);

        return CreatedAtAction(nameof(GetById), new { id = lesson.Id }, new { lessonId = lesson.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLessonRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var lesson = await _dbContext.Lessons.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
        if (lesson is null)
        {
            return NotFound();
        }

        var previousStatus = lesson.Status;
        var previousKind = lesson.Kind;
        var previousEnrollmentId = lesson.EnrollmentId;
        var previousDurationMinutes = lesson.DurationMinutes;
        var changingKind = lesson.Kind != request.Kind;
        var changingEnrollment = lesson.EnrollmentId != request.EnrollmentId;
        if ((changingKind || changingEnrollment) &&
            (previousStatus == LessonStatus.Realized || request.Status == LessonStatus.Realized))
        {
            return BadRequest("Tipo da aula e matrícula vinculada não podem mudar quando a aula está ou fica Realizada.");
        }

        lesson.StudentId = request.StudentId;
        lesson.InstructorId = request.InstructorId;
        lesson.Kind = request.Kind;
        lesson.Status = request.Status;
        lesson.EnrollmentId = request.EnrollmentId;
        lesson.SingleLessonPrice = request.SingleLessonPrice;
        lesson.StartAtUtc = request.StartAtUtc;
        lesson.DurationMinutes = request.DurationMinutes;
        lesson.Notes = NormalizeNullable(request.Notes);

        var validation = await ValidateLessonReferences(request.StudentId, request.InstructorId, schoolId);
        if (validation is IActionResult refError)
        {
            return refError;
        }

        var lessonValidation = await ValidateLessonBusinessRulesAsync(lesson, schoolId);
        if (lessonValidation is IActionResult lessonError)
        {
            return lessonError;
        }

        var schedulingValidation = await _lessonSchedulingService.ValidateAsync(schoolId, lesson, lesson.Id);
        if (!schedulingValidation.IsValid)
        {
            return BadRequest(schedulingValidation.ErrorMessage);
        }

        var settings = schedulingValidation.Settings ?? await _settingsClient.GetAsync(schoolId);
        var transitionResult = await ApplyEnrollmentTransitionAsync(
            schoolId,
            lesson,
            previousStatus,
            lesson.Status,
            settings,
            previousKind,
            previousEnrollmentId,
            previousDurationMinutes);

        if (transitionResult is IActionResult transitionError)
        {
            return transitionError;
        }

        await _dbContext.SaveChangesAsync();

        if (previousKind == LessonKind.Single && lesson.Kind != LessonKind.Single)
        {
            await _financialAutomationService.RemoveSingleLessonRevenueAsync(schoolId, lesson.Id);
        }
        else
        {
            await SyncLessonRevenueAsync(lesson);
        }

        return Ok();
    }

    [HttpPost("{id:guid}/operational-confirm")]
    public async Task<IActionResult> OperationalConfirm(Guid id, [FromBody] OperationalConfirmLessonRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var lesson = await _dbContext.Lessons.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
        if (lesson is null)
        {
            return NotFound();
        }

        lesson.OperationalConfirmedAtUtc = DateTime.UtcNow;
        lesson.OperationalConfirmedByUserId = ResolveCurrentIdentityUserId();
        lesson.OperationalConfirmationNote = NormalizeNullable(request.Note);
        if (lesson.Status == LessonStatus.Scheduled)
        {
            lesson.Status = LessonStatus.Confirmed;
        }

        await _dbContext.SaveChangesAsync();
        await SyncLessonRevenueAsync(lesson);

        return Ok(new
        {
            confirmedAtUtc = lesson.OperationalConfirmedAtUtc,
            lessonStatus = lesson.Status.ToString()
        });
    }

    [HttpPost("{id:guid}/mark-no-show")]
    public async Task<IActionResult> MarkNoShow(Guid id, [FromBody] MarkNoShowLessonRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var lesson = await _dbContext.Lessons.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
        if (lesson is null)
        {
            return NotFound();
        }

        var settings = await _settingsClient.GetAsync(schoolId);
        var previousStatus = lesson.Status;
        lesson.Status = LessonStatus.NoShow;
        lesson.NoShowMarkedAtUtc = DateTime.UtcNow;
        lesson.NoShowMarkedByUserId = ResolveCurrentIdentityUserId();
        lesson.NoShowNote = NormalizeNullable(request.Note);

        var transitionResult = await ApplyEnrollmentTransitionAsync(
            schoolId,
            lesson,
            previousStatus,
            lesson.Status,
            settings);

        if (transitionResult is IActionResult transitionError)
        {
            return transitionError;
        }

        await _dbContext.SaveChangesAsync();
        await SyncLessonRevenueAsync(lesson);

        return Ok(new
        {
            noShowAtUtc = lesson.NoShowMarkedAtUtc,
            consumesCourseMinutes = settings.NoShowConsumesCourseMinutes,
            chargesSingleLesson = settings.NoShowChargesSingleLesson
        });
    }

    [HttpPost("{id:guid}/assisted-rebook")]
    public async Task<IActionResult> GetAssistedRebook(Guid id, [FromBody] AssistedRebookRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var lesson = await _dbContext.Lessons.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
        if (lesson is null)
        {
            return NotFound();
        }

        var suggestions = await _lessonSchedulingService.GetSuggestedSlotsAsync(
            schoolId,
            lesson,
            request.StartSearchAtUtc ?? lesson.StartAtUtc.AddHours(1),
            Math.Clamp(request.DaysToSearch, 1, 21),
            request.InstructorId,
            HttpContext.RequestAborted);

        return Ok(new
        {
            lessonId = lesson.Id,
            slots = suggestions
        });
    }

    [HttpPost("reschedule-batch")]
    public async Task<IActionResult> BatchReschedule([FromBody] BatchRescheduleLessonsRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        if (request.LessonIds is null || request.LessonIds.Length == 0)
        {
            return BadRequest("Informe ao menos uma aula para remarcar.");
        }

        var lessons = await _dbContext.Lessons
            .Where(x => x.SchoolId == schoolId && request.LessonIds.Contains(x.Id))
            .OrderBy(x => x.StartAtUtc)
            .ToListAsync();

        if (lessons.Count != request.LessonIds.Distinct().Count())
        {
            return BadRequest("Uma ou mais aulas selecionadas não foram encontradas.");
        }

        var baseStart = lessons[0].StartAtUtc;
        var settings = await _settingsClient.GetAsync(schoolId);

        foreach (var lesson in lessons)
        {
            var offset = lesson.StartAtUtc - baseStart;
            lesson.StartAtUtc = request.NewStartAtUtc.Add(offset);
            if (request.InstructorId.HasValue)
            {
                lesson.InstructorId = request.InstructorId.Value;
            }

            lesson.Status = LessonStatus.Scheduled;
            var schedulingValidation = await _lessonSchedulingService.ValidateAsync(schoolId, lesson, lesson.Id);
            if (!schedulingValidation.IsValid)
            {
                return BadRequest($"Não foi possível remarcar o lote porque a aula {lesson.Id} ficou inválida: {schedulingValidation.ErrorMessage}");
            }

            lesson.OperationalConfirmedAtUtc = null;
            lesson.OperationalConfirmedByUserId = null;
            lesson.OperationalConfirmationNote = null;
            lesson.NoShowMarkedAtUtc = null;
            lesson.NoShowMarkedByUserId = null;
            lesson.NoShowNote = null;

            var transitionResult = await ApplyEnrollmentTransitionAsync(
                schoolId,
                lesson,
                lesson.Status,
                LessonStatus.Scheduled,
                settings);

            if (transitionResult is IActionResult transitionError)
            {
                return transitionError;
            }
        }

        await _dbContext.SaveChangesAsync();
        foreach (var lesson in lessons)
        {
            await SyncLessonRevenueAsync(lesson);
        }

        return Ok(new
        {
            rescheduled = lessons.Count,
            newBaseStartAtUtc = request.NewStartAtUtc
        });
    }

    private async Task<IActionResult?> ValidateLessonReferences(Guid studentId, Guid instructorId, Guid schoolId)
    {
        var studentExists = await _dbContext.Students.AnyAsync(x =>
            x.Id == studentId &&
            x.SchoolId == schoolId &&
            x.IsActive);

        if (!studentExists)
        {
            return BadRequest("O aluno informado não pertence à escola atual.");
        }

        var instructorExists = await _dbContext.Instructors.AnyAsync(x =>
            x.Id == instructorId &&
            x.SchoolId == schoolId &&
            x.IsActive);

        if (!instructorExists)
        {
            return BadRequest("O instrutor informado não pertence à escola atual.");
        }

        return null;
    }

    private async Task<IActionResult?> ValidateLessonBusinessRulesAsync(Lesson lesson, Guid schoolId)
    {
        if (lesson.DurationMinutes <= 0)
        {
            return BadRequest("A duração da aula precisa ser maior que zero.");
        }

        if (lesson.Kind == LessonKind.Single)
        {
            if (!lesson.SingleLessonPrice.HasValue || lesson.SingleLessonPrice.Value <= 0)
            {
                return BadRequest("Aula avulsa exige um preço próprio maior que zero.");
            }

            lesson.EnrollmentId = null;
            return null;
        }

        lesson.SingleLessonPrice = null;

        if (!lesson.EnrollmentId.HasValue)
        {
            return BadRequest("Aula de curso exige uma matrícula vinculada.");
        }

        var enrollment = await _dbContext.Enrollments.FirstOrDefaultAsync(x =>
            x.Id == lesson.EnrollmentId.Value &&
            x.SchoolId == schoolId &&
            x.StudentId == lesson.StudentId);

        if (enrollment is null)
        {
            return BadRequest("A matrícula informada não pertence ao aluno e à escola atuais.");
        }

        if (enrollment.Status is EnrollmentStatus.Cancelled or EnrollmentStatus.Expired)
        {
            return BadRequest("A matrícula selecionada não pode ser usada porque não está ativa.");
        }

        return null;
    }

    private async Task<IActionResult?> ApplyEnrollmentTransitionAsync(
        Guid schoolId,
        Lesson lesson,
        LessonStatus previousStatus,
        LessonStatus nextStatus,
        SchoolOperationsSettings settings,
        LessonKind? previousKind = null,
        Guid? previousEnrollmentId = null,
        int? previousDurationMinutes = null)
    {
        var effectivePreviousKind = previousKind ?? lesson.Kind;
        var effectivePreviousEnrollmentId = previousEnrollmentId ?? lesson.EnrollmentId;
        var effectivePreviousDurationMinutes = previousDurationMinutes ?? lesson.DurationMinutes;

        var wasCourseChargeable = ShouldConsumeCourseMinutes(effectivePreviousKind, previousStatus, effectivePreviousEnrollmentId, settings);
        var willBeCourseChargeable = ShouldConsumeCourseMinutes(lesson.Kind, nextStatus, lesson.EnrollmentId, settings);

        if (!wasCourseChargeable && !willBeCourseChargeable)
        {
            return null;
        }

        if (wasCourseChargeable &&
            willBeCourseChargeable &&
            effectivePreviousEnrollmentId == lesson.EnrollmentId &&
            effectivePreviousDurationMinutes == lesson.DurationMinutes)
        {
            return null;
        }

        var enrollmentIds = new[]
        {
            effectivePreviousEnrollmentId,
            lesson.EnrollmentId
        }
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();

        var enrollments = await _dbContext.Enrollments
            .Where(x => enrollmentIds.Contains(x.Id) && x.SchoolId == schoolId)
            .ToDictionaryAsync(x => x.Id);

        Enrollment? previousEnrollment = null;
        Enrollment? nextEnrollment = null;

        if (wasCourseChargeable && !enrollments.TryGetValue(effectivePreviousEnrollmentId!.Value, out previousEnrollment))
        {
            return BadRequest("A matrícula vinculada anteriormente à aula não foi encontrada.");
        }

        if (willBeCourseChargeable && !enrollments.TryGetValue(lesson.EnrollmentId!.Value, out nextEnrollment))
        {
            return BadRequest("A matrícula vinculada à aula não foi encontrada.");
        }

        if (willBeCourseChargeable && nextEnrollment!.Status == EnrollmentStatus.Cancelled)
        {
            return BadRequest("Matrículas canceladas não podem consumir saldo.");
        }

        if (!wasCourseChargeable && willBeCourseChargeable)
        {
            var remainingMinutes = Math.Max(0, nextEnrollment!.IncludedMinutesSnapshot - nextEnrollment.UsedMinutes);
            if (remainingMinutes < lesson.DurationMinutes)
            {
                return BadRequest("A matrícula não possui saldo de horas suficiente para realizar essa aula.");
            }

            ApplyEnrollmentUsageDelta(schoolId, nextEnrollment, lesson, -lesson.DurationMinutes, ResolveEnrollmentReason(nextStatus));
        }
        else if (wasCourseChargeable && !willBeCourseChargeable)
        {
            ApplyEnrollmentUsageDelta(schoolId, previousEnrollment!, lesson, effectivePreviousDurationMinutes, "LessonRefunded");
        }
        else if (wasCourseChargeable && willBeCourseChargeable)
        {
            var durationDelta = lesson.DurationMinutes - effectivePreviousDurationMinutes;
            if (durationDelta > 0)
            {
                var remainingMinutes = Math.Max(0, nextEnrollment!.IncludedMinutesSnapshot - nextEnrollment.UsedMinutes);
                if (remainingMinutes < durationDelta)
                {
                    return BadRequest("A matrícula não possui saldo suficiente para ampliar a duração dessa aula.");
                }

                ApplyEnrollmentUsageDelta(schoolId, nextEnrollment, lesson, -durationDelta, "LessonDurationAdjusted");
            }
            else if (durationDelta < 0)
            {
                ApplyEnrollmentUsageDelta(schoolId, nextEnrollment!, lesson, Math.Abs(durationDelta), "LessonDurationAdjusted");
            }
        }

        return null;
    }

    private static bool ShouldConsumeCourseMinutes(
        LessonKind kind,
        LessonStatus status,
        Guid? enrollmentId,
        SchoolOperationsSettings settings)
        => kind == LessonKind.Course &&
           enrollmentId.HasValue &&
           (status == LessonStatus.Realized || (status == LessonStatus.NoShow && settings.NoShowConsumesCourseMinutes));

    private static string ResolveEnrollmentReason(LessonStatus status)
        => status == LessonStatus.NoShow ? "LessonNoShow" : "LessonRealized";

    private void ApplyEnrollmentUsageDelta(
        Guid schoolId,
        Enrollment enrollment,
        Lesson lesson,
        int deltaMinutes,
        string reason)
    {
        enrollment.UsedMinutes = Math.Clamp(
            enrollment.UsedMinutes - deltaMinutes,
            0,
            enrollment.IncludedMinutesSnapshot);

        _dbContext.EnrollmentBalanceLedger.Add(new EnrollmentBalanceLedgerEntry
        {
            SchoolId = schoolId,
            EnrollmentId = enrollment.Id,
            LessonId = lesson.Id == Guid.Empty ? null : lesson.Id,
            DeltaMinutes = deltaMinutes,
            Reason = reason,
            OccurredAtUtc = DateTime.UtcNow
        });

        enrollment.Status = enrollment.UsedMinutes >= enrollment.IncludedMinutesSnapshot
            ? EnrollmentStatus.Completed
            : EnrollmentStatus.Active;

        enrollment.EndedAtUtc = enrollment.Status == EnrollmentStatus.Completed ? DateTime.UtcNow : null;
    }

    private async Task SyncLessonRevenueAsync(Lesson lesson)
    {
        if (lesson.Kind != LessonKind.Single)
        {
            await _financialAutomationService.RemoveSingleLessonRevenueAsync(lesson.SchoolId, lesson.Id);
            return;
        }

        var student = await _dbContext.Students.FirstOrDefaultAsync(x => x.Id == lesson.StudentId && x.SchoolId == lesson.SchoolId);
        var instructor = await _dbContext.Instructors.FirstOrDefaultAsync(x => x.Id == lesson.InstructorId && x.SchoolId == lesson.SchoolId);
        if (student is null || instructor is null)
        {
            return;
        }

        await _financialAutomationService.SyncSingleLessonRevenueAsync(lesson, student, instructor);
    }

    private Guid? ResolveCurrentIdentityUserId()
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claimValue, out var parsed) ? parsed : null;
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public sealed record CreateLessonRequest(
        Guid StudentId,
        Guid InstructorId,
        LessonKind Kind,
        LessonStatus Status,
        Guid? EnrollmentId,
        decimal? SingleLessonPrice,
        DateTime StartAtUtc,
        int DurationMinutes,
        string? Notes);

    public sealed record UpdateLessonRequest(
        Guid StudentId,
        Guid InstructorId,
        LessonKind Kind,
        LessonStatus Status,
        Guid? EnrollmentId,
        decimal? SingleLessonPrice,
        DateTime StartAtUtc,
        int DurationMinutes,
        string? Notes);

    public sealed record OperationalConfirmLessonRequest(string? Note);

    public sealed record MarkNoShowLessonRequest(string? Note);

    public sealed record AssistedRebookRequest(DateTime? StartSearchAtUtc, int DaysToSearch = 7, Guid? InstructorId = null);

    public sealed record BatchRescheduleLessonsRequest(Guid[] LessonIds, DateTime NewStartAtUtc, Guid? InstructorId = null);

    public sealed record CreateScheduleBlockRequest(
        ScheduleBlockScope Scope,
        Guid? InstructorId,
        string Title,
        string? Notes,
        DateTime StartAtUtc,
        DateTime EndAtUtc);
}
