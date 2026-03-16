using KiteFlow.BuildingBlocks.MultiTenancy;
using KiteFlow.Services.Schools.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace KiteFlow.Services.Schools.Api.Controllers;

[ApiController]
[Authorize(Policy = "SchoolMembers")]
[Route("api/v1/schools")]
public sealed class SchoolsController : ControllerBase
{
    private readonly SchoolsDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;

    public SchoolsController(SchoolsDbContext dbContext, ICurrentTenant currentTenant)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent()
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;
        var isStaff =
            User.IsInRole("SystemAdmin") ||
            User.IsInRole("Owner") ||
            User.IsInRole("Instructor");

        var school = await _dbContext.Schools.FirstOrDefaultAsync(x => x.Id == schoolId);
        if (school is null)
        {
            return NotFound();
        }

        var settings = await _dbContext.SchoolSettings.FirstOrDefaultAsync(x => x.SchoolId == schoolId);
        object? profiles = null;
        if (isStaff)
        {
            profiles = await _dbContext.UserProfiles
                .Where(x => x.SchoolId == schoolId)
                .OrderBy(x => x.FullName)
                .Select(x => new
                {
                    x.Id,
                    x.IdentityUserId,
                    x.FullName,
                    x.Phone,
                    x.IsActive
                })
                .ToListAsync();
        }

        return Ok(new
        {
            school.Id,
            school.LegalName,
            school.DisplayName,
            school.Slug,
            school.LogoDataUrl,
            school.Status,
            school.Timezone,
            school.CurrencyCode,
            settings,
            users = profiles
        });
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUserProfile()
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var identityUserId))
        {
            return Unauthorized();
        }

        var profile = await _dbContext.UserProfiles
            .Where(x => x.SchoolId == schoolId && x.IdentityUserId == identityUserId)
            .Select(x => new
            {
                x.Id,
                x.IdentityUserId,
                x.FullName,
                x.Phone,
                x.AvatarUrl,
                x.IsActive
            })
            .FirstOrDefaultAsync();

        if (profile is null)
        {
            return NotFound();
        }

        return Ok(profile);
    }

    [HttpGet("portal-settings")]
    public async Task<IActionResult> GetPortalSettings()
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var settings = await _dbContext.SchoolSettings
            .Where(x => x.SchoolId == schoolId)
            .Select(x => new
            {
                x.BookingLeadTimeMinutes,
                x.CancellationWindowHours,
                x.RescheduleWindowHours,
                x.AttendanceConfirmationLeadMinutes,
                x.LessonReminderLeadHours,
                x.PortalNotificationsEnabled,
                x.InstructorBufferMinutes,
                x.NoShowGraceMinutes,
                x.NoShowConsumesCourseMinutes,
                x.NoShowChargesSingleLesson,
                x.AutoCreateEnrollmentRevenue,
                x.AutoCreateSingleLessonRevenue,
                x.ThemePrimary,
                x.ThemeAccent
            })
            .FirstOrDefaultAsync();

        if (settings is null)
        {
            return NotFound();
        }

        return Ok(settings);
    }

    [Authorize(Policy = "SchoolManagementAccess")]
    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateSchoolSettingsRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        if (request.BookingLeadTimeMinutes < 0)
        {
            return BadRequest("A antecedência mínima não pode ser negativa.");
        }

        if (request.CancellationWindowHours < 0)
        {
            return BadRequest("A janela de cancelamento não pode ser negativa.");
        }

        if (request.RescheduleWindowHours < 0)
        {
            return BadRequest("A janela de remarcação não pode ser negativa.");
        }

        if (request.AttendanceConfirmationLeadMinutes < 0)
        {
            return BadRequest("A antecedência para confirmação de presença não pode ser negativa.");
        }

        if (request.LessonReminderLeadHours < 0)
        {
            return BadRequest("A antecedência do lembrete não pode ser negativa.");
        }

        if (request.InstructorBufferMinutes < 0)
        {
            return BadRequest("O buffer entre aulas não pode ser negativo.");
        }

        if (request.NoShowGraceMinutes < 0)
        {
            return BadRequest("A tolerância para no-show não pode ser negativa.");
        }

        var settings = await _dbContext.SchoolSettings.FirstOrDefaultAsync(x => x.SchoolId == schoolId);
        if (settings is null)
        {
            return NotFound();
        }

        settings.BookingLeadTimeMinutes = request.BookingLeadTimeMinutes;
        settings.CancellationWindowHours = request.CancellationWindowHours;
        settings.RescheduleWindowHours = request.RescheduleWindowHours;
        settings.AttendanceConfirmationLeadMinutes = request.AttendanceConfirmationLeadMinutes;
        settings.LessonReminderLeadHours = request.LessonReminderLeadHours;
        settings.PortalNotificationsEnabled = request.PortalNotificationsEnabled;
        settings.InstructorBufferMinutes = request.InstructorBufferMinutes;
        settings.NoShowGraceMinutes = request.NoShowGraceMinutes;
        settings.NoShowConsumesCourseMinutes = request.NoShowConsumesCourseMinutes;
        settings.NoShowChargesSingleLesson = request.NoShowChargesSingleLesson;
        settings.AutoCreateEnrollmentRevenue = request.AutoCreateEnrollmentRevenue;
        settings.AutoCreateSingleLessonRevenue = request.AutoCreateSingleLessonRevenue;
        settings.ThemePrimary = NormalizeTheme(request.ThemePrimary, settings.ThemePrimary);
        settings.ThemeAccent = NormalizeTheme(request.ThemeAccent, settings.ThemeAccent);

        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            updatedAtUtc = DateTime.UtcNow,
            settings.BookingLeadTimeMinutes,
            settings.CancellationWindowHours,
            settings.RescheduleWindowHours,
            settings.AttendanceConfirmationLeadMinutes,
            settings.LessonReminderLeadHours,
            settings.PortalNotificationsEnabled,
            settings.InstructorBufferMinutes,
            settings.NoShowGraceMinutes,
            settings.NoShowConsumesCourseMinutes,
            settings.NoShowChargesSingleLesson,
            settings.AutoCreateEnrollmentRevenue,
            settings.AutoCreateSingleLessonRevenue,
            settings.ThemePrimary,
            settings.ThemeAccent
        });
    }

    private static string NormalizeTheme(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    public sealed record UpdateSchoolSettingsRequest(
        int BookingLeadTimeMinutes,
        int CancellationWindowHours,
        int RescheduleWindowHours,
        int AttendanceConfirmationLeadMinutes,
        int LessonReminderLeadHours,
        bool PortalNotificationsEnabled,
        int InstructorBufferMinutes,
        int NoShowGraceMinutes,
        bool NoShowConsumesCourseMinutes,
        bool NoShowChargesSingleLesson,
        bool AutoCreateEnrollmentRevenue,
        bool AutoCreateSingleLessonRevenue,
        string? ThemePrimary,
        string? ThemeAccent);
}
