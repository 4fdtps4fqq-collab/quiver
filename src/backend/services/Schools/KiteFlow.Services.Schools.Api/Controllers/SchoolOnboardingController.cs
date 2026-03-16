using KiteFlow.Services.Schools.Api.Data;
using KiteFlow.Services.Schools.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace KiteFlow.Services.Schools.Api.Controllers;

[ApiController]
[Route("api/v1/onboarding")]
public sealed class SchoolOnboardingController : ControllerBase
{
    private readonly SchoolsDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public SchoolOnboardingController(SchoolsDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    [Authorize(Policy = "SystemAdminOnly")]
    [HttpPost("register-school")]
    public async Task<IActionResult> RegisterSchool([FromBody] RegisterSchoolRequest request)
    {
        var legalName = (request.LegalName ?? string.Empty).Trim();
        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? legalName
            : request.DisplayName.Trim();
        var ownerFullName = (request.OwnerFullName ?? string.Empty).Trim();
        var slug = BuildSlug(request.Slug, displayName);

        if (string.IsNullOrWhiteSpace(legalName))
        {
            return BadRequest("A razão social da escola é obrigatória.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return BadRequest("O nome de exibição da escola é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(ownerFullName))
        {
            return BadRequest("O nome completo do responsável pela escola é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(request.BaseBeachName))
        {
            return BadRequest("O nome da praia base da escola é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(request.OwnerCpf))
        {
            return BadRequest("O CPF do proprietário é obrigatório.");
        }

        if (ValidateLogo(request.LogoDataUrl) is IActionResult logoError)
        {
            return logoError;
        }

        var slugExists = await _dbContext.Schools.AnyAsync(x => x.Slug == slug);
        if (slugExists)
        {
            return Conflict("Ja existe uma escola cadastrada com esse identificador.");
        }

        var school = new School
        {
            Id = request.SchoolId,
            LegalName = legalName,
            DisplayName = displayName,
            Cnpj = TrimToNull(request.Cnpj),
            BaseBeachName = TrimToNull(request.BaseBeachName),
            BaseLatitude = request.BaseLatitude,
            BaseLongitude = request.BaseLongitude,
            Slug = slug,
            Timezone = string.IsNullOrWhiteSpace(request.Timezone) ? "America/Sao_Paulo" : request.Timezone.Trim(),
            CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode) ? "BRL" : request.CurrencyCode.Trim().ToUpperInvariant(),
            LogoDataUrl = NormalizeLogo(request.LogoDataUrl),
            PostalCode = TrimToNull(request.PostalCode),
            Street = TrimToNull(request.Street),
            StreetNumber = TrimToNull(request.StreetNumber),
            AddressComplement = TrimToNull(request.AddressComplement),
            Neighborhood = TrimToNull(request.Neighborhood),
            City = TrimToNull(request.City),
            State = NormalizeState(request.State),
            Status = SchoolStatus.Active
        };

        var settings = new SchoolSettings
        {
            SchoolId = school.Id,
            ThemePrimary = string.IsNullOrWhiteSpace(request.ThemePrimary) ? "#0E3A52" : request.ThemePrimary.Trim(),
            ThemeAccent = string.IsNullOrWhiteSpace(request.ThemeAccent) ? "#FFB703" : request.ThemeAccent.Trim(),
            BookingLeadTimeMinutes = request.BookingLeadTimeMinutes <= 0 ? 60 : request.BookingLeadTimeMinutes,
            CancellationWindowHours = request.CancellationWindowHours <= 0 ? 24 : request.CancellationWindowHours,
            RescheduleWindowHours = 24,
            AttendanceConfirmationLeadMinutes = 180,
            LessonReminderLeadHours = 18,
            PortalNotificationsEnabled = true,
            InstructorBufferMinutes = 15,
            NoShowGraceMinutes = 15,
            NoShowConsumesCourseMinutes = true,
            NoShowChargesSingleLesson = true,
            AutoCreateEnrollmentRevenue = true,
            AutoCreateSingleLessonRevenue = true
        };

        var ownerProfile = new UserProfile
        {
            SchoolId = school.Id,
            IdentityUserId = request.OwnerIdentityUserId,
            FullName = ownerFullName,
            Cpf = TrimToNull(request.OwnerCpf),
            Phone = TrimToNull(request.OwnerPhone),
            PostalCode = TrimToNull(request.OwnerPostalCode),
            Street = TrimToNull(request.OwnerStreet),
            StreetNumber = TrimToNull(request.OwnerStreetNumber),
            AddressComplement = TrimToNull(request.OwnerAddressComplement),
            Neighborhood = TrimToNull(request.OwnerNeighborhood),
            City = TrimToNull(request.OwnerCity),
            State = NormalizeState(request.OwnerState),
            IsActive = true
        };

        _dbContext.Schools.Add(school);
        _dbContext.SchoolSettings.Add(settings);
        _dbContext.UserProfiles.Add(ownerProfile);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(RegisterSchool), new { schoolId = school.Id }, new
        {
            school.Id,
            school.LegalName,
            school.DisplayName,
            school.Slug,
            ownerProfile.IdentityUserId
        });
    }

    [AllowAnonymous]
    [HttpPost("register-invited-user")]
    public async Task<IActionResult> RegisterInvitedUser([FromBody] RegisterInvitedUserRequest request)
    {
        if (!IsInternalGatewayCall())
        {
            return Unauthorized("Esta rota interna aceita apenas chamadas autenticadas pelo gateway.");
        }

        var fullName = (request.FullName ?? string.Empty).Trim();
        if (request.SchoolId == Guid.Empty)
        {
            return BadRequest("O identificador da escola é obrigatório.");
        }

        if (request.IdentityUserId == Guid.Empty)
        {
            return BadRequest("O identificador do usuário no Identity é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return BadRequest("O nome completo da pessoa convidada é obrigatório.");
        }

        var schoolExists = await _dbContext.Schools.AnyAsync(x => x.Id == request.SchoolId);
        if (!schoolExists)
        {
            return NotFound("Escola não encontrada.");
        }

        var existingProfile = await _dbContext.UserProfiles.FirstOrDefaultAsync(x =>
            x.SchoolId == request.SchoolId &&
            x.IdentityUserId == request.IdentityUserId);

        if (existingProfile is not null)
        {
            return Ok(new
            {
                existingProfile.Id,
                existingProfile.IdentityUserId,
                existingProfile.FullName
            });
        }

        var profile = new UserProfile
        {
            SchoolId = request.SchoolId,
            IdentityUserId = request.IdentityUserId,
            FullName = fullName,
            Phone = request.Phone?.Trim(),
            IsActive = true
        };

        _dbContext.UserProfiles.Add(profile);
        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            profile.Id,
            profile.IdentityUserId,
            profile.FullName
        });
    }

    private static string BuildSlug(string? rawSlug, string displayName)
    {
        var source = string.IsNullOrWhiteSpace(rawSlug) ? displayName : rawSlug;
        var normalized = new string(source
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray());

        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }

        normalized = normalized.Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? $"school-{Guid.NewGuid():N}"[..14] : normalized;
    }

    private static string? NormalizeLogo(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeState(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static IActionResult? ValidateLogo(string? logoDataUrl)
    {
        if (string.IsNullOrWhiteSpace(logoDataUrl))
        {
            return null;
        }

        if (!logoDataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            return new BadRequestObjectResult("A logo da escola precisa ser enviada como imagem válida.");
        }

        if (logoDataUrl.Length > 3_000_000)
        {
            return new BadRequestObjectResult("A logo da escola excede o tamanho permitido.");
        }

        return null;
    }

    public sealed record RegisterSchoolRequest(
        Guid SchoolId,
        Guid OwnerIdentityUserId,
        string LegalName,
        string DisplayName,
        string? Cnpj,
        string? BaseBeachName,
        double? BaseLatitude,
        double? BaseLongitude,
        string? PostalCode,
        string? Street,
        string? StreetNumber,
        string? AddressComplement,
        string? Neighborhood,
        string? City,
        string? State,
        string OwnerFullName,
        string? OwnerCpf,
        string? OwnerPhone,
        string? OwnerPostalCode,
        string? OwnerStreet,
        string? OwnerStreetNumber,
        string? OwnerAddressComplement,
        string? OwnerNeighborhood,
        string? OwnerCity,
        string? OwnerState,
        string? Slug,
        string? Timezone,
        string? CurrencyCode,
        string? LogoDataUrl,
        string? ThemePrimary,
        string? ThemeAccent,
        int BookingLeadTimeMinutes,
        int CancellationWindowHours);

    public sealed record RegisterInvitedUserRequest(
        Guid SchoolId,
        Guid IdentityUserId,
        string FullName,
        string? Phone);

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
