using KiteFlow.Services.Schools.Api.Data;
using KiteFlow.Services.Schools.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Schools.Api.Controllers;

[ApiController]
[Authorize(Policy = "SystemAdminOnly")]
[Route("api/v1/system/schools")]
public sealed class SystemSchoolsController : ControllerBase
{
    private readonly SchoolsDbContext _dbContext;

    public SystemSchoolsController(SchoolsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> ListSchools(CancellationToken cancellationToken)
    {
        var schools = await _dbContext.Schools
            .AsNoTracking()
            .OrderBy(x => x.DisplayName)
            .Select(x => new
            {
                x.Id,
                x.LegalName,
                x.DisplayName,
                x.Slug,
                x.LogoDataUrl,
                x.BaseBeachName,
                x.BaseLatitude,
                x.BaseLongitude,
                status = x.Status.ToString(),
                x.Timezone,
                x.CurrencyCode,
                x.CreatedAtUtc,
                usersCount = _dbContext.UserProfiles.Count(profile => profile.SchoolId == x.Id),
                ownerName = _dbContext.UserProfiles
                    .Where(profile => profile.SchoolId == x.Id)
                    .OrderBy(profile => profile.CreatedAtUtc)
                    .Select(profile => profile.FullName)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return Ok(schools);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetSchool(Guid id, CancellationToken cancellationToken)
    {
        var school = await _dbContext.Schools
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.LegalName,
                x.DisplayName,
                x.Cnpj,
                x.BaseBeachName,
                x.BaseLatitude,
                x.BaseLongitude,
                x.LogoDataUrl,
                x.PostalCode,
                x.Street,
                x.StreetNumber,
                x.AddressComplement,
                x.Neighborhood,
                x.City,
                x.State,
                x.Timezone,
                x.CurrencyCode,
                status = x.Status.ToString(),
                owner = _dbContext.UserProfiles
                    .Where(profile => profile.SchoolId == x.Id)
                    .OrderBy(profile => profile.CreatedAtUtc)
                    .Select(profile => new
                    {
                        profile.Id,
                        profile.IdentityUserId,
                        profile.FullName,
                        profile.Cpf,
                        profile.Phone,
                        profile.PostalCode,
                        profile.Street,
                        profile.StreetNumber,
                        profile.AddressComplement,
                        profile.Neighborhood,
                        profile.City,
                        profile.State,
                        profile.IsActive
                    })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (school is null)
        {
            return NotFound();
        }

        return Ok(school);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateSchool(Guid id, [FromBody] UpdateSystemSchoolRequest request, CancellationToken cancellationToken)
    {
        var school = await _dbContext.Schools.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (school is null)
        {
            return NotFound();
        }

        var ownerProfile = await _dbContext.UserProfiles
            .Where(x => x.SchoolId == id)
            .OrderBy(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (ownerProfile is null)
        {
            return BadRequest("Não foi possível localizar o perfil do proprietário inicial desta escola.");
        }

        var legalName = (request.LegalName ?? string.Empty).Trim();
        var displayName = (request.DisplayName ?? string.Empty).Trim();
        var ownerFullName = (request.OwnerFullName ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(legalName))
        {
            return BadRequest("A razão social da escola é obrigatória.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return BadRequest("O nome de exibição da escola é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(request.BaseBeachName))
        {
            return BadRequest("O nome da praia base da escola é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(ownerFullName))
        {
            return BadRequest("O nome do proprietário é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(request.OwnerCpf))
        {
            return BadRequest("O CPF do proprietário é obrigatório.");
        }

        if (ValidateLogo(request.LogoDataUrl) is IActionResult logoError)
        {
            return logoError;
        }

        if (!TryParseSchoolStatus(request.Status, out var schoolStatus))
        {
            return BadRequest("O status informado para a escola é inválido.");
        }

        school.LegalName = legalName;
        school.DisplayName = displayName;
        school.Cnpj = NormalizeNullable(request.Cnpj);
        school.BaseBeachName = NormalizeNullable(request.BaseBeachName);
        school.BaseLatitude = request.BaseLatitude;
        school.BaseLongitude = request.BaseLongitude;
        school.LogoDataUrl = NormalizeNullable(request.LogoDataUrl);
        school.PostalCode = NormalizeNullable(request.PostalCode);
        school.Street = NormalizeNullable(request.Street);
        school.StreetNumber = NormalizeNullable(request.StreetNumber);
        school.AddressComplement = NormalizeNullable(request.AddressComplement);
        school.Neighborhood = NormalizeNullable(request.Neighborhood);
        school.City = NormalizeNullable(request.City);
        school.State = NormalizeState(request.State);
        school.Status = schoolStatus;
        school.Timezone = string.IsNullOrWhiteSpace(request.Timezone) ? school.Timezone : request.Timezone.Trim();
        school.CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode)
            ? school.CurrencyCode
            : request.CurrencyCode.Trim().ToUpperInvariant();

        ownerProfile.FullName = ownerFullName;
        ownerProfile.Cpf = NormalizeNullable(request.OwnerCpf);
        ownerProfile.Phone = NormalizeNullable(request.OwnerPhone);
        ownerProfile.PostalCode = NormalizeNullable(request.OwnerPostalCode);
        ownerProfile.Street = NormalizeNullable(request.OwnerStreet);
        ownerProfile.StreetNumber = NormalizeNullable(request.OwnerStreetNumber);
        ownerProfile.AddressComplement = NormalizeNullable(request.OwnerAddressComplement);
        ownerProfile.Neighborhood = NormalizeNullable(request.OwnerNeighborhood);
        ownerProfile.City = NormalizeNullable(request.OwnerCity);
        ownerProfile.State = NormalizeState(request.OwnerState);
        ownerProfile.IsActive = request.OwnerIsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            updatedAtUtc = DateTime.UtcNow,
            school.Id
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteSchool(Guid id, CancellationToken cancellationToken)
    {
        var school = await _dbContext.Schools.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (school is null)
        {
            return NotFound();
        }

        var settings = await _dbContext.SchoolSettings.Where(x => x.SchoolId == id).ToListAsync(cancellationToken);
        var profiles = await _dbContext.UserProfiles.Where(x => x.SchoolId == id).ToListAsync(cancellationToken);

        if (settings.Count > 0)
        {
            _dbContext.SchoolSettings.RemoveRange(settings);
        }

        if (profiles.Count > 0)
        {
            _dbContext.UserProfiles.RemoveRange(profiles);
        }

        _dbContext.Schools.Remove(school);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            deletedAtUtc = DateTime.UtcNow,
            schoolId = id
        });
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeState(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static bool TryParseSchoolStatus(string? value, out SchoolStatus status)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            status = SchoolStatus.Active;
            return true;
        }

        return Enum.TryParse(value.Trim(), true, out status);
    }

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

    public sealed record UpdateSystemSchoolRequest(
        string LegalName,
        string DisplayName,
        string? Cnpj,
        string BaseBeachName,
        double? BaseLatitude,
        double? BaseLongitude,
        string? LogoDataUrl,
        string PostalCode,
        string Street,
        string StreetNumber,
        string? AddressComplement,
        string Neighborhood,
        string City,
        string State,
        string OwnerFullName,
        string OwnerCpf,
        string? OwnerPhone,
        string OwnerPostalCode,
        string OwnerStreet,
        string OwnerStreetNumber,
        string? OwnerAddressComplement,
        string OwnerNeighborhood,
        string OwnerCity,
        string OwnerState,
        bool OwnerIsActive,
        string Status,
        string? Timezone,
        string? CurrencyCode);
}
