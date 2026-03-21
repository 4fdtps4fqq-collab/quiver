using KiteFlow.BuildingBlocks.MultiTenancy;
using KiteFlow.Services.Schools.Api.Data;
using KiteFlow.Services.Schools.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Schools.Api.Controllers;

[ApiController]
[Authorize(Policy = "SchoolManagementAccess")]
[Route("api/v1/users")]
public sealed class UsersController : ControllerBase
{
    private readonly SchoolsDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;

    public UsersController(SchoolsDbContext dbContext, ICurrentTenant currentTenant)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = false)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var query = _dbContext.UserProfiles.Where(x => x.SchoolId == schoolId);
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        var items = await query
            .OrderBy(x => x.FullName)
            .Select(x => new
            {
                x.Id,
                x.IdentityUserId,
                x.FullName,
                x.Phone,
                x.SalaryAmount,
                x.AvatarUrl,
                x.IsActive,
                x.CreatedAtUtc
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserProfileRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var fullName = (request.FullName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return BadRequest("O nome completo do usuário é obrigatório.");
        }

        if (request.SalaryAmount.HasValue && request.SalaryAmount.Value < 0)
        {
            return BadRequest("O salário não pode ser negativo.");
        }

        var exists = await _dbContext.UserProfiles.AnyAsync(x =>
            x.SchoolId == schoolId &&
            x.IdentityUserId == request.IdentityUserId);

        if (exists)
        {
            return Conflict("Esse usuário do Identity já está vinculado a esta escola.");
        }

        var profile = new UserProfile
        {
            SchoolId = schoolId,
            IdentityUserId = request.IdentityUserId,
            FullName = fullName,
            Phone = NormalizeNullable(request.Phone),
            SalaryAmount = NormalizeSalary(request.SalaryAmount),
            AvatarUrl = NormalizeNullable(request.AvatarUrl),
            IsActive = request.IsActive
        };

        _dbContext.UserProfiles.Add(profile);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { id = profile.Id }, new
        {
            profile.Id,
            profile.IdentityUserId
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserProfileRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var profile = await _dbContext.UserProfiles.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
        if (profile is null)
        {
            return NotFound();
        }

        var fullName = (request.FullName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return BadRequest("O nome completo do usuário é obrigatório.");
        }

        if (request.SalaryAmount.HasValue && request.SalaryAmount.Value < 0)
        {
            return BadRequest("O salário não pode ser negativo.");
        }

        profile.FullName = fullName;
        profile.Phone = NormalizeNullable(request.Phone);
        profile.SalaryAmount = NormalizeSalary(request.SalaryAmount);
        profile.AvatarUrl = NormalizeNullable(request.AvatarUrl);
        profile.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync();
        return Ok();
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static decimal? NormalizeSalary(decimal? value)
        => value.HasValue ? decimal.Round(value.Value, 2) : null;

    public sealed record CreateUserProfileRequest(
        Guid IdentityUserId,
        string FullName,
        string? Phone,
        decimal? SalaryAmount,
        string? AvatarUrl,
        bool IsActive);

    public sealed record UpdateUserProfileRequest(
        string FullName,
        string? Phone,
        decimal? SalaryAmount,
        string? AvatarUrl,
        bool IsActive);
}
