using System.Security.Cryptography;
using System.Text;
using KiteFlow.Services.Academics.Api.Data;
using KiteFlow.Services.Academics.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Academics.Api.Controllers;

[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/v1/internal/academics")]
public sealed class InternalAcademicsFinancialAnalyticsController : ControllerBase
{
    private readonly AcademicsDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public InternalAcademicsFinancialAnalyticsController(AcademicsDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    [HttpGet("financial-analytics")]
    public async Task<IActionResult> GetFinancialAnalytics(
        [FromQuery] Guid schoolId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        if (!IsInternalServiceCall())
        {
            return Unauthorized("Esta rota interna aceita apenas chamadas autenticadas entre serviços.");
        }

        if (schoolId == Guid.Empty)
        {
            return BadRequest("O identificador da escola é obrigatório.");
        }

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            return BadRequest("A data inicial deve ser menor ou igual à data final.");
        }

        var lessonsQuery = _dbContext.Lessons
            .Where(x => x.SchoolId == schoolId && x.Status == LessonStatus.Realized);

        if (fromUtc.HasValue)
        {
            lessonsQuery = lessonsQuery.Where(x => x.StartAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            lessonsQuery = lessonsQuery.Where(x => x.StartAtUtc <= toUtc.Value);
        }

        var lessons = await lessonsQuery
            .Select(x => new
            {
                x.Id,
                x.InstructorId,
                x.EnrollmentId,
                x.Kind,
                x.SingleLessonPrice,
                x.DurationMinutes,
                x.StartAtUtc
            })
            .ToListAsync(cancellationToken);

        var enrollmentIds = lessons
            .Where(x => x.EnrollmentId.HasValue)
            .Select(x => x.EnrollmentId!.Value)
            .Distinct()
            .ToList();

        var enrollments = await _dbContext.Enrollments
            .Where(x => x.SchoolId == schoolId && enrollmentIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.CourseId,
                x.CoursePriceSnapshot,
                x.IncludedMinutesSnapshot,
                x.StartedAtUtc
            })
            .ToListAsync(cancellationToken);

        var enrollmentMap = enrollments.ToDictionary(x => x.Id);

        var courseIds = enrollments
            .Select(x => x.CourseId)
            .Distinct()
            .ToList();

        var courses = await _dbContext.Courses
            .Where(x => x.SchoolId == schoolId && courseIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name })
            .ToListAsync(cancellationToken);

        var courseNames = courses.ToDictionary(x => x.Id, x => x.Name);

        var instructors = await _dbContext.Instructors
            .Where(x => x.SchoolId == schoolId)
            .Select(x => new { x.Id, x.FullName, x.HourlyRate })
            .ToListAsync(cancellationToken);

        var instructorMap = instructors.ToDictionary(x => x.Id);

        var startedEnrollmentsQuery = _dbContext.Enrollments.Where(x => x.SchoolId == schoolId);
        if (fromUtc.HasValue)
        {
            startedEnrollmentsQuery = startedEnrollmentsQuery.Where(x => x.StartedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            startedEnrollmentsQuery = startedEnrollmentsQuery.Where(x => x.StartedAtUtc <= toUtc.Value);
        }

        var startedEnrollments = await startedEnrollmentsQuery
            .Select(x => new
            {
                x.CourseId,
                x.CoursePriceSnapshot
            })
            .ToListAsync(cancellationToken);

        var courseRecognizedRevenue = startedEnrollments
            .GroupBy(x => x.CourseId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(item => item.CoursePriceSnapshot));

        var courseEnrollmentsStarted = startedEnrollments
            .GroupBy(x => x.CourseId)
            .ToDictionary(
                group => group.Key,
                group => group.Count());

        var byInstructor = new Dictionary<Guid, InstructorMarginAccumulator>();
        var byCourse = new Dictionary<string, CourseMarginAccumulator>(StringComparer.OrdinalIgnoreCase);

        foreach (var lesson in lessons)
        {
            if (!instructorMap.TryGetValue(lesson.InstructorId, out var instructor))
            {
                continue;
            }

            var payrollExpense = Math.Round(((decimal)lesson.DurationMinutes / 60m) * instructor.HourlyRate, 2);
            var deliveredRevenue = 0m;
            string courseKey;
            Guid? courseId = null;
            string courseName;

            if (lesson.Kind == LessonKind.Single || !lesson.EnrollmentId.HasValue)
            {
                deliveredRevenue = lesson.SingleLessonPrice ?? 0m;
                courseKey = "single";
                courseName = "Aula avulsa";
            }
            else if (enrollmentMap.TryGetValue(lesson.EnrollmentId.Value, out var enrollment))
            {
                courseId = enrollment.CourseId;
                courseName = courseNames.TryGetValue(enrollment.CourseId, out var knownCourseName)
                    ? knownCourseName
                    : "Curso";
                courseKey = enrollment.CourseId.ToString();

                if (enrollment.IncludedMinutesSnapshot > 0)
                {
                    deliveredRevenue = Math.Round(
                        enrollment.CoursePriceSnapshot * lesson.DurationMinutes / enrollment.IncludedMinutesSnapshot,
                        2);
                }
            }
            else
            {
                courseKey = "unknown";
                courseName = "Curso";
            }

            if (!byInstructor.TryGetValue(instructor.Id, out var instructorAccumulator))
            {
                instructorAccumulator = new InstructorMarginAccumulator(instructor.Id, instructor.FullName);
                byInstructor[instructor.Id] = instructorAccumulator;
            }

            instructorAccumulator.RealizedLessons += 1;
            instructorAccumulator.RealizedMinutes += lesson.DurationMinutes;
            instructorAccumulator.DeliveredRevenue += deliveredRevenue;
            instructorAccumulator.PayrollExpense += payrollExpense;

            if (!byCourse.TryGetValue(courseKey, out var courseAccumulator))
            {
                courseAccumulator = new CourseMarginAccumulator(courseId, courseName);
                byCourse[courseKey] = courseAccumulator;
            }

            courseAccumulator.RealizedLessons += 1;
            courseAccumulator.RealizedMinutes += lesson.DurationMinutes;
            courseAccumulator.DeliveredRevenue += deliveredRevenue;
            courseAccumulator.PayrollExpense += payrollExpense;
        }

        foreach (var course in courses)
        {
            var key = course.Id.ToString();
            if (!byCourse.TryGetValue(key, out var accumulator))
            {
                accumulator = new CourseMarginAccumulator(course.Id, course.Name);
                byCourse[key] = accumulator;
            }

            accumulator.RecognizedRevenue = courseRecognizedRevenue.TryGetValue(course.Id, out var revenue)
                ? revenue
                : accumulator.RecognizedRevenue;
            accumulator.EnrollmentsStarted = courseEnrollmentsStarted.TryGetValue(course.Id, out var count)
                ? count
                : accumulator.EnrollmentsStarted;
        }

        if (byCourse.TryGetValue("single", out var singleLessonAccumulator))
        {
            singleLessonAccumulator.RecognizedRevenue = singleLessonAccumulator.DeliveredRevenue;
        }

        var byCourseItems = byCourse.Values
            .Select(x => new
            {
                x.CourseId,
                x.CourseName,
                x.EnrollmentsStarted,
                x.RealizedLessons,
                x.RealizedMinutes,
                recognizedRevenue = Math.Round(x.RecognizedRevenue, 2),
                deliveredRevenue = Math.Round(x.DeliveredRevenue, 2),
                payrollExpense = Math.Round(x.PayrollExpense, 2),
                grossMargin = Math.Round(x.RecognizedRevenue - x.PayrollExpense, 2)
            })
            .OrderByDescending(x => x.recognizedRevenue + x.deliveredRevenue)
            .ThenBy(x => x.CourseName)
            .ToList();

        var byInstructorItems = byInstructor.Values
            .Select(x => new
            {
                x.InstructorId,
                x.InstructorName,
                x.RealizedLessons,
                x.RealizedMinutes,
                deliveredRevenue = Math.Round(x.DeliveredRevenue, 2),
                payrollExpense = Math.Round(x.PayrollExpense, 2),
                grossMargin = Math.Round(x.DeliveredRevenue - x.PayrollExpense, 2)
            })
            .OrderByDescending(x => x.deliveredRevenue)
            .ThenBy(x => x.InstructorName)
            .ToList();

        return Ok(new
        {
            schoolId,
            fromUtc,
            toUtc,
            byCourse = byCourseItems,
            byInstructor = byInstructorItems
        });
    }

    private bool IsInternalServiceCall()
    {
        var expected = _configuration["InternalServiceAuth:SharedKey"];
        var provided = Request.Headers["X-KiteFlow-Internal-Key"].ToString();

        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(provided))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(provided));
    }

    private sealed class CourseMarginAccumulator
    {
        public CourseMarginAccumulator(Guid? courseId, string courseName)
        {
            CourseId = courseId;
            CourseName = courseName;
        }

        public Guid? CourseId { get; }
        public string CourseName { get; }
        public int EnrollmentsStarted { get; set; }
        public int RealizedLessons { get; set; }
        public int RealizedMinutes { get; set; }
        public decimal RecognizedRevenue { get; set; }
        public decimal DeliveredRevenue { get; set; }
        public decimal PayrollExpense { get; set; }
    }

    private sealed class InstructorMarginAccumulator
    {
        public InstructorMarginAccumulator(Guid instructorId, string instructorName)
        {
            InstructorId = instructorId;
            InstructorName = instructorName;
        }

        public Guid InstructorId { get; }
        public string InstructorName { get; }
        public int RealizedLessons { get; set; }
        public int RealizedMinutes { get; set; }
        public decimal DeliveredRevenue { get; set; }
        public decimal PayrollExpense { get; set; }
    }
}
