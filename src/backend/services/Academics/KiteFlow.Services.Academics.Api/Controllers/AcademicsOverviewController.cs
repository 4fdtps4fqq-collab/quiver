using KiteFlow.BuildingBlocks.MultiTenancy;
using KiteFlow.Services.Academics.Api.Data;
using KiteFlow.Services.Academics.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Academics.Api.Controllers;

[ApiController]
[Authorize(Policy = "DashboardAccess")]
[Route("api/v1/academics")]
public sealed class AcademicsOverviewController : ControllerBase
{
    private readonly AcademicsDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;

    public AcademicsOverviewController(AcademicsDbContext dbContext, ICurrentTenant currentTenant)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            return BadRequest("A data inicial deve ser menor ou igual a data final.");
        }

        var activeEnrollments = await _dbContext.Enrollments.CountAsync(x =>
            x.SchoolId == schoolId && x.Status == EnrollmentStatus.Active);

        var lessonsQuery = _dbContext.Lessons.Where(x => x.SchoolId == schoolId);
        if (fromUtc.HasValue)
        {
            lessonsQuery = lessonsQuery.Where(x => x.StartAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            lessonsQuery = lessonsQuery.Where(x => x.StartAtUtc <= toUtc.Value);
        }

        var scheduledLessons = await lessonsQuery.CountAsync(x => x.Status == LessonStatus.Scheduled);

        var realizedLessons = await lessonsQuery.CountAsync(x => x.Status == LessonStatus.Realized);

        var todayStart = DateTime.UtcNow.Date;
        var tomorrowStart = todayStart.AddDays(1);

        var realizedInstructionRows = await lessonsQuery
            .Where(x => x.Status == LessonStatus.Realized)
            .Join(
                _dbContext.Instructors.Where(x => x.SchoolId == schoolId),
                lesson => lesson.InstructorId,
                instructor => instructor.Id,
                (lesson, instructor) => new
                {
                    lesson.InstructorId,
                    instructor.FullName,
                    lesson.StartAtUtc,
                    lesson.DurationMinutes,
                    instructor.HourlyRate
                })
            .ToListAsync();

        var realizedInstructionData = realizedInstructionRows
            .Select(item => new
            {
                item.InstructorId,
                item.FullName,
                item.StartAtUtc,
                item.DurationMinutes,
                item.HourlyRate,
                payrollExpense = Math.Round(((decimal)item.DurationMinutes / 60m) * item.HourlyRate, 2)
            })
            .ToList();

        var lessonStats = await lessonsQuery
            .GroupBy(x => x.Status)
            .Select(g => new
            {
                status = g.Key.ToString(),
                count = g.Count()
            })
            .ToListAsync();

        var instructorPerformance = await lessonsQuery
            .GroupBy(x => x.InstructorId)
            .Select(g => new
            {
                instructorId = g.Key,
                total = g.Count(),
                realized = g.Count(x => x.Status == LessonStatus.Realized),
                noShow = g.Count(x => x.Status == LessonStatus.NoShow),
                cancelled = g.Count(x => x.Status == LessonStatus.Cancelled || x.Status == LessonStatus.CancelledByWind),
                rescheduled = g.Count(x => x.Status == LessonStatus.Rescheduled)
            })
            .ToListAsync();

        var lessonSeries = await lessonsQuery
            .GroupBy(x => x.StartAtUtc.Date)
            .Select(g => new
            {
                day = g.Key,
                totalLessons = g.Count(),
                realizedLessons = g.Count(x => x.Status == LessonStatus.Realized),
                cancelledLessons = g.Count(x =>
                    x.Status == LessonStatus.Cancelled ||
                    x.Status == LessonStatus.CancelledByWind ||
                    x.Status == LessonStatus.NoShow)
            })
            .OrderBy(x => x.day)
            .ToListAsync();

        var totalLessonsInPeriod = lessonSeries.Sum(x => x.totalLessons);
        var realizedInstructionMinutes = realizedInstructionData.Sum(x => x.DurationMinutes);
        var instructorPayrollExpense = realizedInstructionData.Sum(x => x.payrollExpense);

        var instructorNames = await _dbContext.Instructors
            .Where(x => x.SchoolId == schoolId)
            .Select(x => new { x.Id, x.FullName })
            .ToListAsync();

        var instructorPayrollSeries = realizedInstructionData
            .GroupBy(x => x.StartAtUtc.Date)
            .OrderBy(x => x.Key)
            .Select(group => new
            {
                bucketStartUtc = group.Key,
                bucketLabel = group.Key.ToString("dd/MM"),
                realizedInstructionMinutes = group.Sum(x => x.DurationMinutes),
                instructorPayrollExpense = group.Sum(x => x.payrollExpense)
            });

        var instructorPayrollBreakdown = realizedInstructionData
            .GroupBy(x => new { x.InstructorId, x.FullName })
            .OrderByDescending(x => x.Sum(item => item.payrollExpense))
            .Select(group => new
            {
                instructorId = group.Key.InstructorId,
                instructorName = group.Key.FullName,
                realizedInstructionMinutes = group.Sum(x => x.DurationMinutes),
                payrollExpense = group.Sum(x => x.payrollExpense)
            });

        return Ok(new
        {
            students = await _dbContext.Students.CountAsync(x => x.SchoolId == schoolId && x.IsActive),
            instructors = await _dbContext.Instructors.CountAsync(x => x.SchoolId == schoolId && x.IsActive),
            courses = await _dbContext.Courses.CountAsync(x => x.SchoolId == schoolId && x.IsActive),
            activeEnrollments,
            fromUtc,
            toUtc,
            scheduledLessons,
            realizedLessons,
            totalLessonsInPeriod,
            completionRate = totalLessonsInPeriod == 0
                ? 0
                : Math.Round((decimal)realizedLessons / totalLessonsInPeriod * 100m, 1),
            lessonsToday = await _dbContext.Lessons.CountAsync(x =>
                x.SchoolId == schoolId &&
                x.StartAtUtc >= todayStart &&
                x.StartAtUtc < tomorrowStart),
            statusBreakdown = lessonStats,
            lessonSeries = lessonSeries.Select(x => new
            {
                bucketStartUtc = x.day,
                bucketLabel = x.day.ToString("dd/MM"),
                x.totalLessons,
                x.realizedLessons,
                x.cancelledLessons
            }),
            realizedInstructionMinutes,
            instructorPayrollExpense,
            instructorPayrollSeries,
            instructorPayrollBreakdown,
            instructorPerformance = instructorPerformance
                .OrderByDescending(x => x.realized)
                .Select(x => new
                {
                    x.instructorId,
                    instructorName = instructorNames.FirstOrDefault(i => i.Id == x.instructorId)?.FullName ?? "Instructor",
                    x.total,
                    x.realized,
                    x.noShow,
                    x.cancelled,
                    x.rescheduled
                }),
            invariants = new[]
            {
                "Aulas de curso exigem matrícula vinculada e só consomem saldo horário quando ficam como realizadas.",
                "Se uma aula de curso sair do status de realizada, a carga horária da matrícula volta por meio de um movimento compensatório.",
                "Aulas avulsas mantêm preço próprio e não alteram o saldo das matrículas."
            }
        });
    }
}
