using KiteFlow.Services.Schools.Api.Data;
using KiteFlow.Services.Schools.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace KiteFlow.Services.Schools.Api.Controllers;

[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/v1/internal/schools")]
public sealed class InternalSchoolsController : ControllerBase
{
    private readonly SchoolsDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public InternalSchoolsController(SchoolsDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    [HttpGet("{id:guid}/access")]
    public async Task<IActionResult> GetSchoolAccess(Guid id, CancellationToken cancellationToken)
    {
        if (!IsInternalGatewayCall())
        {
            return Unauthorized("Esta rota interna aceita apenas chamadas autenticadas entre serviços.");
        }

        var school = await _dbContext.Schools
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.DisplayName,
                status = x.Status.ToString(),
                isAccessAllowed = x.Status == SchoolStatus.Active
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (school is null)
        {
            return NotFound("Escola não encontrada.");
        }

        return Ok(school);
    }

    [HttpGet("{id:guid}/operations-settings")]
    public async Task<IActionResult> GetOperationsSettings(Guid id, CancellationToken cancellationToken)
    {
        if (!IsInternalGatewayCall())
        {
            return Unauthorized("Esta rota interna aceita apenas chamadas autenticadas entre serviços.");
        }

        var settings = await _dbContext.SchoolSettings
            .AsNoTracking()
            .Where(x => x.SchoolId == id)
            .Select(x => new
            {
                x.BookingLeadTimeMinutes,
                x.CancellationWindowHours,
                x.RescheduleWindowHours,
                x.AttendanceConfirmationLeadMinutes,
                x.LessonReminderLeadHours,
                x.PortalNotificationsEnabled,
                x.ThemePrimary,
                x.ThemeAccent,
                x.InstructorBufferMinutes,
                x.NoShowGraceMinutes,
                x.NoShowConsumesCourseMinutes,
                x.NoShowChargesSingleLesson,
                x.AutoCreateEnrollmentRevenue,
                x.AutoCreateSingleLessonRevenue
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            return NotFound("Configurações da escola não encontradas.");
        }

        return Ok(settings);
    }

    private bool IsInternalGatewayCall()
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
}
