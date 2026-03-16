using KiteFlow.BuildingBlocks.MultiTenancy;
using KiteFlow.Services.Academics.Api.Data;
using KiteFlow.Services.Academics.Api.Domain;
using KiteFlow.Services.Academics.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Academics.Api.Controllers;

[ApiController]
[Route("api/v1/instructors")]
public sealed class InstructorsController : ControllerBase
{
    private readonly AcademicsDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;

    public InstructorsController(AcademicsDbContext dbContext, ICurrentTenant currentTenant)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
    }

    [Authorize(Policy = "InstructorsReadAccess")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = true)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var query = _dbContext.Instructors.Where(x => x.SchoolId == schoolId);
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        var items = await query
            .OrderBy(x => x.FullName)
            .Select(x => new
            {
                x.Id,
                x.FullName,
                x.Email,
                x.Phone,
                x.Specialties,
                availability = LessonSchedulingService.ParseAvailability(x.AvailabilityJson),
                x.HourlyRate,
                x.IdentityUserId,
                x.IsActive,
                x.CreatedAtUtc
            })
            .ToListAsync();

        return Ok(items);
    }

    [Authorize(Policy = "InstructorsAccess")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertInstructorRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;
        var email = NormalizeEmail(request.Email);

        var fullName = (request.FullName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return BadRequest("O nome completo do instrutor é obrigatório.");
        }

        if (request.HourlyRate < 0)
        {
            return BadRequest("O valor da hora/aula não pode ser negativo.");
        }

        var activeConflict = await FindActiveInstructorConflictAsync(
            schoolId,
            null,
            request.IdentityUserId,
            email);

        if (activeConflict)
        {
            return Conflict("Já existe um instrutor ativo vinculado a esse acesso ou e-mail em outra escola.");
        }

        var instructor = new Instructor
        {
            SchoolId = schoolId,
            FullName = fullName,
            Email = email,
            Phone = NormalizeNullable(request.Phone),
            Specialties = NormalizeNullable(request.Specialties),
            AvailabilityJson = LessonSchedulingService.SerializeAvailability(request.Availability),
            HourlyRate = decimal.Round(request.HourlyRate, 2),
            IdentityUserId = request.IdentityUserId,
            IsActive = true
        };

        _dbContext.Instructors.Add(instructor);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { id = instructor.Id }, new { instructorId = instructor.Id });
    }

    [Authorize(Policy = "InstructorsAccess")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateInstructorRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;
        var email = NormalizeEmail(request.Email);

        var instructor = await _dbContext.Instructors.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
        if (instructor is null)
        {
            return NotFound();
        }

        var fullName = (request.FullName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return BadRequest("O nome completo do instrutor é obrigatório.");
        }

        if (request.HourlyRate < 0)
        {
            return BadRequest("O valor da hora/aula não pode ser negativo.");
        }

        if (request.IsActive)
        {
            var activeConflict = await FindActiveInstructorConflictAsync(
                schoolId,
                id,
                request.IdentityUserId,
                email);

            if (activeConflict)
            {
                return Conflict("Já existe um instrutor ativo vinculado a esse acesso ou e-mail em outra escola.");
            }
        }

        instructor.FullName = fullName;
        instructor.Email = email;
        instructor.Phone = NormalizeNullable(request.Phone);
        instructor.Specialties = NormalizeNullable(request.Specialties);
        instructor.AvailabilityJson = LessonSchedulingService.SerializeAvailability(request.Availability);
        instructor.HourlyRate = decimal.Round(request.HourlyRate, 2);
        instructor.IdentityUserId = request.IdentityUserId;
        instructor.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync();
        return Ok();
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeEmail(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private async Task<bool> FindActiveInstructorConflictAsync(
        Guid schoolId,
        Guid? currentInstructorId,
        Guid? identityUserId,
        string? email)
    {
        return await _dbContext.Instructors.AnyAsync(x =>
            x.IsActive &&
            x.Id != currentInstructorId &&
            x.SchoolId != schoolId &&
            (
                (identityUserId.HasValue && x.IdentityUserId == identityUserId.Value) ||
                (!string.IsNullOrWhiteSpace(email) && x.Email != null && EF.Functions.ILike(x.Email, email))
            ));
    }

    public sealed record UpsertInstructorRequest(
        string FullName,
        string? Email,
        string? Phone,
        string? Specialties,
        IReadOnlyCollection<InstructorAvailabilitySlotModel>? Availability,
        decimal HourlyRate,
        Guid? IdentityUserId);

    public sealed record UpdateInstructorRequest(
        string FullName,
        string? Email,
        string? Phone,
        string? Specialties,
        IReadOnlyCollection<InstructorAvailabilitySlotModel>? Availability,
        decimal HourlyRate,
        Guid? IdentityUserId,
        bool IsActive);
}
