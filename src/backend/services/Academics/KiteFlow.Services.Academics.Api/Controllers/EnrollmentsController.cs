using KiteFlow.BuildingBlocks.MultiTenancy;
using KiteFlow.Services.Academics.Api.Data;
using KiteFlow.Services.Academics.Api.Domain;
using KiteFlow.Services.Academics.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Academics.Api.Controllers;

[ApiController]
[Route("api/v1/enrollments")]
public sealed class EnrollmentsController : ControllerBase
{
    private readonly AcademicsDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;
    private readonly FinancialAutomationService _financialAutomationService;

    public EnrollmentsController(
        AcademicsDbContext dbContext,
        ICurrentTenant currentTenant,
        FinancialAutomationService financialAutomationService)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
        _financialAutomationService = financialAutomationService;
    }

    [Authorize(Policy = "EnrollmentsReadAccess")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? studentId, [FromQuery] EnrollmentStatus? status)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var query = _dbContext.Enrollments
            .Where(x => x.SchoolId == schoolId)
            .Include(x => x.Student)
            .Include(x => x.Course)
            .AsQueryable();

        if (studentId.HasValue)
        {
            query = query.Where(x => x.StudentId == studentId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.StudentId,
                studentName = x.Student!.FullName,
                x.CourseId,
                courseName = x.Course!.Name,
                status = x.Status.ToString(),
                x.IncludedMinutesSnapshot,
                x.UsedMinutes,
                remainingMinutes = Math.Max(0, x.IncludedMinutesSnapshot - x.UsedMinutes),
                x.CoursePriceSnapshot,
                x.StartedAtUtc,
                x.EndedAtUtc,
                progressPercent = x.IncludedMinutesSnapshot == 0
                    ? 0m
                    : Math.Round((decimal)x.UsedMinutes / x.IncludedMinutesSnapshot * 100m, 1),
                realizedLessons = _dbContext.Lessons.Count(l => l.SchoolId == schoolId && l.EnrollmentId == x.Id && l.Status == LessonStatus.Realized),
                noShowCount = _dbContext.Lessons.Count(l => l.SchoolId == schoolId && l.EnrollmentId == x.Id && l.Status == LessonStatus.NoShow)
            })
            .ToListAsync();

        return Ok(items.Select(x => new
        {
            x.Id,
            x.StudentId,
            x.studentName,
            x.CourseId,
            x.courseName,
            x.status,
            x.IncludedMinutesSnapshot,
            x.UsedMinutes,
            x.remainingMinutes,
            x.CoursePriceSnapshot,
            x.StartedAtUtc,
            x.EndedAtUtc,
            x.progressPercent,
            x.realizedLessons,
            x.noShowCount,
            currentModule = ResolveCurrentModule(x.progressPercent)
        }));
    }

    [Authorize(Policy = "EnrollmentsAccess")]
    [HttpGet("{id:guid}/ledger")]
    public async Task<IActionResult> GetLedger(Guid id)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var enrollmentExists = await _dbContext.Enrollments.AnyAsync(x => x.Id == id && x.SchoolId == schoolId);
        if (!enrollmentExists)
        {
            return NotFound();
        }

        var items = await _dbContext.EnrollmentBalanceLedger
            .Where(x => x.SchoolId == schoolId && x.EnrollmentId == id)
            .OrderBy(x => x.OccurredAtUtc)
            .Select(x => new
            {
                x.Id,
                x.LessonId,
                x.DeltaMinutes,
                x.Reason,
                x.OccurredAtUtc
            })
            .ToListAsync();

        return Ok(items);
    }

    [Authorize(Policy = "EnrollmentsAccess")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEnrollmentRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var student = await _dbContext.Students.FirstOrDefaultAsync(x =>
            x.Id == request.StudentId &&
            x.SchoolId == schoolId &&
            x.IsActive);

        if (student is null)
        {
            return BadRequest("O aluno informado não pertence à escola atual.");
        }

        var course = await _dbContext.Courses.FirstOrDefaultAsync(x =>
            x.Id == request.CourseId &&
            x.SchoolId == schoolId &&
            x.IsActive);

        if (course is null)
        {
            return BadRequest("O curso informado não pertence à escola atual.");
        }

        var duplicateActive = await _dbContext.Enrollments.AnyAsync(x =>
            x.SchoolId == schoolId &&
            x.StudentId == request.StudentId &&
            x.CourseId == request.CourseId &&
            x.Status == EnrollmentStatus.Active);

        if (duplicateActive)
        {
            return Conflict("Já existe uma matrícula ativa desse aluno para esse curso.");
        }

        var enrollment = new Enrollment
        {
            SchoolId = schoolId,
            StudentId = request.StudentId,
            CourseId = request.CourseId,
            Status = EnrollmentStatus.Active,
            IncludedMinutesSnapshot = course.TotalMinutes,
            UsedMinutes = 0,
            CoursePriceSnapshot = course.Price,
            StartedAtUtc = request.StartedAtUtc ?? DateTime.UtcNow
        };

        _dbContext.Enrollments.Add(enrollment);
        await _dbContext.SaveChangesAsync();
        await _financialAutomationService.SyncEnrollmentRevenueAsync(enrollment, student, course);

        return CreatedAtAction(nameof(GetAll), new { id = enrollment.Id }, new
        {
            enrollmentId = enrollment.Id,
            enrollment.IncludedMinutesSnapshot,
            enrollment.CoursePriceSnapshot
        });
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateEnrollmentStatusRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var enrollment = await _dbContext.Enrollments.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
        if (enrollment is null)
        {
            return NotFound();
        }

        enrollment.Status = request.Status;
        if (request.Status is EnrollmentStatus.Cancelled or EnrollmentStatus.Completed or EnrollmentStatus.Expired)
        {
            enrollment.EndedAtUtc = request.EndedAtUtc ?? DateTime.UtcNow;
        }
        else
        {
            enrollment.EndedAtUtc = null;
        }

        await _dbContext.SaveChangesAsync();
        var student = await _dbContext.Students.FirstAsync(x => x.Id == enrollment.StudentId && x.SchoolId == schoolId);
        var course = await _dbContext.Courses.FirstAsync(x => x.Id == enrollment.CourseId && x.SchoolId == schoolId);
        await _financialAutomationService.SyncEnrollmentRevenueAsync(enrollment, student, course);
        return Ok();
    }

    private static string ResolveCurrentModule(decimal progressPercent)
        => progressPercent switch
        {
            < 25m => "Base técnica",
            < 50m => "Controle do kite",
            < 80m => "Prancha e navegação",
            _ => "Autonomia"
        };

    public sealed record CreateEnrollmentRequest(Guid StudentId, Guid CourseId, DateTime? StartedAtUtc);

    public sealed record UpdateEnrollmentStatusRequest(EnrollmentStatus Status, DateTime? EndedAtUtc);
}
