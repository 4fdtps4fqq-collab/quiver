using KiteFlow.BuildingBlocks.MultiTenancy;
using KiteFlow.Services.Academics.Api.Data;
using KiteFlow.Services.Academics.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Academics.Api.Controllers;

[ApiController]
[Route("api/v1/students")]
public sealed class StudentsController : ControllerBase
{
    private readonly AcademicsDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;

    public StudentsController(AcademicsDbContext dbContext, ICurrentTenant currentTenant)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
    }

    [Authorize(Policy = "StudentsReadAccess")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = true)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var query = _dbContext.Students.Where(x => x.SchoolId == schoolId);
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
                x.PostalCode,
                x.Street,
                x.StreetNumber,
                x.AddressComplement,
                x.Neighborhood,
                x.City,
                x.State,
                x.IdentityUserId,
                x.BirthDate,
                x.MedicalNotes,
                x.EmergencyContactName,
                x.EmergencyContactPhone,
                x.FirstStandUpAtUtc,
                x.IsActive,
                x.CreatedAtUtc,
                activeEnrollments = _dbContext.Enrollments.Count(e => e.SchoolId == schoolId && e.StudentId == x.Id && e.Status == EnrollmentStatus.Active),
                realizedLessons = _dbContext.Lessons.Count(l => l.SchoolId == schoolId && l.StudentId == x.Id && l.Status == LessonStatus.Realized),
                upcomingLessons = _dbContext.Lessons.Count(l => l.SchoolId == schoolId && l.StudentId == x.Id && l.StartAtUtc >= DateTime.UtcNow && (l.Status == LessonStatus.Scheduled || l.Status == LessonStatus.Confirmed)),
                noShowCount = _dbContext.Lessons.Count(l => l.SchoolId == schoolId && l.StudentId == x.Id && l.Status == LessonStatus.NoShow),
                progressPercent = _dbContext.Enrollments
                    .Where(e => e.SchoolId == schoolId && e.StudentId == x.Id && e.IncludedMinutesSnapshot > 0)
                    .Select(e => (decimal?)e.UsedMinutes / e.IncludedMinutesSnapshot * 100m)
                    .Average() ?? 0m
            })
            .ToListAsync();

        return Ok(items);
    }

    [Authorize(Policy = "StudentsAccess")]
    [Authorize(Policy = "StudentsAccess")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertStudentRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;
        var email = NormalizeEmail(request.Email);

        var fullName = (request.FullName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return BadRequest("O nome completo do aluno é obrigatório.");
        }

        var activeConflict = await FindActiveStudentConflictAsync(
            schoolId,
            null,
            request.IdentityUserId,
            email);

        if (activeConflict)
        {
            return Conflict("Já existe um aluno ativo vinculado a esse acesso ou e-mail em outra escola.");
        }

        var student = new Student
        {
            SchoolId = schoolId,
            FullName = fullName,
            Email = email,
            Phone = NormalizeNullable(request.Phone),
            PostalCode = NormalizeNullable(request.PostalCode),
            Street = NormalizeNullable(request.Street),
            StreetNumber = NormalizeNullable(request.StreetNumber),
            AddressComplement = NormalizeNullable(request.AddressComplement),
            Neighborhood = NormalizeNullable(request.Neighborhood),
            City = NormalizeNullable(request.City),
            State = NormalizeNullable(request.State),
            IdentityUserId = request.IdentityUserId,
            BirthDate = request.BirthDate,
            MedicalNotes = NormalizeNullable(request.MedicalNotes),
            EmergencyContactName = NormalizeNullable(request.EmergencyContactName),
            EmergencyContactPhone = NormalizeNullable(request.EmergencyContactPhone),
            FirstStandUpAtUtc = request.FirstStandUpAtUtc,
            IsActive = true
        };

        _dbContext.Students.Add(student);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { id = student.Id }, new { studentId = student.Id });
    }

    [Authorize(Policy = "StudentsAccess")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStudentRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;
        var email = NormalizeEmail(request.Email);

        var student = await _dbContext.Students.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
        if (student is null)
        {
            return NotFound();
        }

        var fullName = (request.FullName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return BadRequest("O nome completo do aluno é obrigatório.");
        }

        if (request.IsActive)
        {
            var activeConflict = await FindActiveStudentConflictAsync(
                schoolId,
                id,
                request.IdentityUserId,
                email);

            if (activeConflict)
            {
                return Conflict("Já existe um aluno ativo vinculado a esse acesso ou e-mail em outra escola.");
            }
        }

        student.FullName = fullName;
        student.Email = email;
        student.Phone = NormalizeNullable(request.Phone);
        student.PostalCode = NormalizeNullable(request.PostalCode);
        student.Street = NormalizeNullable(request.Street);
        student.StreetNumber = NormalizeNullable(request.StreetNumber);
        student.AddressComplement = NormalizeNullable(request.AddressComplement);
        student.Neighborhood = NormalizeNullable(request.Neighborhood);
        student.City = NormalizeNullable(request.City);
        student.State = NormalizeNullable(request.State);
        student.IdentityUserId = request.IdentityUserId;
        student.BirthDate = request.BirthDate;
        student.MedicalNotes = NormalizeNullable(request.MedicalNotes);
        student.EmergencyContactName = NormalizeNullable(request.EmergencyContactName);
        student.EmergencyContactPhone = NormalizeNullable(request.EmergencyContactPhone);
        student.FirstStandUpAtUtc = request.FirstStandUpAtUtc;
        student.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync();
        return Ok();
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeEmail(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private async Task<bool> FindActiveStudentConflictAsync(
        Guid schoolId,
        Guid? currentStudentId,
        Guid? identityUserId,
        string? email)
    {
        return await _dbContext.Students.AnyAsync(x =>
            x.IsActive &&
            x.Id != currentStudentId &&
            x.SchoolId != schoolId &&
            (
                (identityUserId.HasValue && x.IdentityUserId == identityUserId.Value) ||
                (!string.IsNullOrWhiteSpace(email) && x.Email != null && EF.Functions.ILike(x.Email, email))
            ));
    }

    public sealed record UpsertStudentRequest(
        string FullName,
        string? Email,
        string? Phone,
        string? PostalCode,
        string? Street,
        string? StreetNumber,
        string? AddressComplement,
        string? Neighborhood,
        string? City,
        string? State,
        Guid? IdentityUserId,
        DateOnly? BirthDate,
        string? MedicalNotes,
        string? EmergencyContactName,
        string? EmergencyContactPhone,
        DateTime? FirstStandUpAtUtc);

    public sealed record UpdateStudentRequest(
        string FullName,
        string? Email,
        string? Phone,
        string? PostalCode,
        string? Street,
        string? StreetNumber,
        string? AddressComplement,
        string? Neighborhood,
        string? City,
        string? State,
        Guid? IdentityUserId,
        DateOnly? BirthDate,
        string? MedicalNotes,
        string? EmergencyContactName,
        string? EmergencyContactPhone,
        DateTime? FirstStandUpAtUtc,
        bool IsActive);
}
