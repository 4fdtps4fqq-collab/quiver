using KiteFlow.Services.Academics.Api.Data;
using KiteFlow.Services.Academics.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace KiteFlow.Services.Academics.Api.Controllers;

[ApiController]
[Route("api/v1/onboarding")]
public sealed class AcademicOnboardingController : ControllerBase
{
    private readonly AcademicsDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public AcademicOnboardingController(AcademicsDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    [AllowAnonymous]
    [HttpPost("register-invited-student")]
    public async Task<IActionResult> RegisterInvitedStudent([FromBody] RegisterInvitedStudentRequest request)
    {
        if (!IsInternalGatewayCall())
        {
            return Unauthorized("Esta rota interna aceita apenas chamadas autenticadas pelo gateway.");
        }

        var fullName = (request.FullName ?? string.Empty).Trim();
        var email = NormalizeEmail(request.Email);

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
            return BadRequest("O nome completo do aluno é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest("O e-mail do aluno é obrigatório.");
        }

        var activeConflict = await _dbContext.Students.AnyAsync(x =>
            x.IsActive &&
            x.SchoolId != request.SchoolId &&
            (
                x.IdentityUserId == request.IdentityUserId ||
                (x.Email != null && EF.Functions.ILike(x.Email, email))
            ));

        if (activeConflict)
        {
            return Conflict("Já existe um aluno ativo vinculado a esse acesso ou e-mail em outra escola.");
        }

        var student = await _dbContext.Students.FirstOrDefaultAsync(x =>
            x.SchoolId == request.SchoolId &&
            x.IdentityUserId == request.IdentityUserId);

        if (student is null)
        {
            student = await _dbContext.Students.FirstOrDefaultAsync(x =>
                x.SchoolId == request.SchoolId &&
                x.Email != null &&
                EF.Functions.ILike(x.Email, email));
        }

        if (student is null)
        {
            student = new Student
            {
                SchoolId = request.SchoolId,
                FullName = fullName,
                Email = email,
                Phone = NormalizeNullable(request.Phone),
                IdentityUserId = request.IdentityUserId,
                IsActive = true
            };

            _dbContext.Students.Add(student);
        }
        else
        {
            student.FullName = fullName;
            student.Email = email;
            student.Phone = NormalizeNullable(request.Phone) ?? student.Phone;
            student.IdentityUserId = request.IdentityUserId;
            student.IsActive = true;
        }

        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            student.Id,
            student.SchoolId,
            student.IdentityUserId,
            student.FullName,
            student.Email
        });
    }

    [AllowAnonymous]
    [HttpPost("register-invited-instructor")]
    public async Task<IActionResult> RegisterInvitedInstructor([FromBody] RegisterInvitedInstructorRequest request)
    {
        if (!IsInternalGatewayCall())
        {
            return Unauthorized("Esta rota interna aceita apenas chamadas autenticadas pelo gateway.");
        }

        var fullName = (request.FullName ?? string.Empty).Trim();
        var email = NormalizeEmail(request.Email);

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
            return BadRequest("O nome completo do instrutor é obrigatório.");
        }

        var activeConflict = await _dbContext.Instructors.AnyAsync(x =>
            x.IsActive &&
            x.SchoolId != request.SchoolId &&
            (
                x.IdentityUserId == request.IdentityUserId ||
                (!string.IsNullOrWhiteSpace(email) && x.Email != null && EF.Functions.ILike(x.Email, email))
            ));

        if (activeConflict)
        {
            return Conflict("Já existe um instrutor ativo vinculado a esse acesso ou e-mail em outra escola.");
        }

        var instructor = await _dbContext.Instructors.FirstOrDefaultAsync(x =>
            x.SchoolId == request.SchoolId &&
            x.IdentityUserId == request.IdentityUserId);

        if (instructor is null && !string.IsNullOrWhiteSpace(email))
        {
            instructor = await _dbContext.Instructors.FirstOrDefaultAsync(x =>
                x.SchoolId == request.SchoolId &&
                x.Email != null &&
                EF.Functions.ILike(x.Email, email));
        }

        if (instructor is null)
        {
            instructor = new Instructor
            {
                SchoolId = request.SchoolId,
                FullName = fullName,
                Email = email,
                Phone = NormalizeNullable(request.Phone),
                IdentityUserId = request.IdentityUserId,
                HourlyRate = 0m,
                IsActive = true
            };

            _dbContext.Instructors.Add(instructor);
        }
        else
        {
            instructor.FullName = fullName;
            instructor.Email = email;
            instructor.Phone = NormalizeNullable(request.Phone) ?? instructor.Phone;
            instructor.IdentityUserId = request.IdentityUserId;
            instructor.IsActive = true;
        }

        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            instructor.Id,
            instructor.SchoolId,
            instructor.IdentityUserId,
            instructor.FullName,
            instructor.Email
        });
    }

    [AllowAnonymous]
    [HttpPost("sync-linked-user-state")]
    public async Task<IActionResult> SyncLinkedUserState([FromBody] SyncLinkedUserStateRequest request)
    {
        if (!IsInternalGatewayCall())
        {
            return Unauthorized("Esta rota interna aceita apenas chamadas autenticadas pelo gateway.");
        }

        if (request.SchoolId == Guid.Empty || request.IdentityUserId == Guid.Empty)
        {
            return BadRequest("Escola e usuário são obrigatórios.");
        }

        var students = await _dbContext.Students
            .Where(x => x.SchoolId == request.SchoolId && x.IdentityUserId == request.IdentityUserId)
            .ToListAsync();

        var instructors = await _dbContext.Instructors
            .Where(x => x.SchoolId == request.SchoolId && x.IdentityUserId == request.IdentityUserId)
            .ToListAsync();

        foreach (var student in students)
        {
            student.IsActive = request.IsActive;
        }

        foreach (var instructor in instructors)
        {
            instructor.IsActive = request.IsActive;
        }

        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            studentsUpdated = students.Count,
            instructorsUpdated = instructors.Count,
            request.IsActive
        });
    }

    [AllowAnonymous]
    [HttpPost("deactivation-impact")]
    public async Task<IActionResult> GetDeactivationImpact([FromBody] DeactivationImpactRequest request)
    {
        if (!IsInternalGatewayCall())
        {
            return Unauthorized("Esta rota interna aceita apenas chamadas autenticadas pelo gateway.");
        }

        if (request.SchoolId == Guid.Empty || request.IdentityUserId == Guid.Empty)
        {
            return BadRequest("Escola e usuário são obrigatórios.");
        }

        var instructor = await _dbContext.Instructors.FirstOrDefaultAsync(x =>
            x.SchoolId == request.SchoolId &&
            x.IdentityUserId == request.IdentityUserId);

        var student = await _dbContext.Students.FirstOrDefaultAsync(x =>
            x.SchoolId == request.SchoolId &&
            x.IdentityUserId == request.IdentityUserId);

        var futureInstructorLessons = instructor is null
            ? 0
            : await _dbContext.Lessons.CountAsync(x =>
                x.SchoolId == request.SchoolId &&
                x.InstructorId == instructor.Id &&
                x.StartAtUtc >= DateTime.UtcNow &&
                x.Status != LessonStatus.Cancelled &&
                x.Status != LessonStatus.NoShow);

        var futureStudentLessons = student is null
            ? 0
            : await _dbContext.Lessons.CountAsync(x =>
                x.SchoolId == request.SchoolId &&
                x.StudentId == student.Id &&
                x.StartAtUtc >= DateTime.UtcNow &&
                x.Status != LessonStatus.Cancelled &&
                x.Status != LessonStatus.NoShow);

        var activeStudentEnrollments = student is null
            ? 0
            : await _dbContext.Enrollments.CountAsync(x =>
                x.SchoolId == request.SchoolId &&
                x.StudentId == student.Id &&
                x.Status == EnrollmentStatus.Active);

        var messages = new List<string>();
        if (futureInstructorLessons > 0)
        {
            messages.Add("Este instrutor possui aulas futuras agendadas e não pode ser desativado até a agenda ser tratada.");
        }

        if (futureStudentLessons > 0)
        {
            messages.Add("Este aluno possui aulas futuras agendadas e não pode ser desativado até a agenda ser tratada.");
        }

        if (activeStudentEnrollments > 0)
        {
            messages.Add("Este aluno possui matrículas ativas e não pode ser desativado até o encerramento dessas matrículas.");
        }

        return Ok(new
        {
            canDeactivate = messages.Count == 0,
            futureInstructorLessons,
            futureStudentLessons,
            activeStudentEnrollments,
            messages
        });
    }

    private static string NormalizeEmail(string? email)
        => (email ?? string.Empty).Trim().ToLowerInvariant();

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public sealed record RegisterInvitedStudentRequest(
        Guid SchoolId,
        Guid IdentityUserId,
        string FullName,
        string Email,
        string? Phone);

    public sealed record RegisterInvitedInstructorRequest(
        Guid SchoolId,
        Guid IdentityUserId,
        string FullName,
        string? Email,
        string? Phone);

    public sealed record SyncLinkedUserStateRequest(
        Guid SchoolId,
        Guid IdentityUserId,
        bool IsActive);

    public sealed record DeactivationImpactRequest(
        Guid SchoolId,
        Guid IdentityUserId);

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
