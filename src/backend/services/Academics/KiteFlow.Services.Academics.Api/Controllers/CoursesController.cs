using KiteFlow.BuildingBlocks.MultiTenancy;
using KiteFlow.Services.Academics.Api.Data;
using KiteFlow.Services.Academics.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Academics.Api.Controllers;

[ApiController]
[Authorize(Policy = "CoursesAccess")]
[Route("api/v1/courses")]
public sealed class CoursesController : ControllerBase
{
    private readonly AcademicsDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;

    public CoursesController(AcademicsDbContext dbContext, ICurrentTenant currentTenant)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var items = await _dbContext.Courses
            .Where(x => x.SchoolId == schoolId)
            .OrderBy(x => x.Level)
            .Select(x => new
            {
                x.Id,
                x.Name,
                level = x.Level.ToString(),
                x.TotalMinutes,
                totalHours = Math.Round(x.TotalMinutes / 60m, 2),
                x.Price,
                x.IsActive
            })
            .ToListAsync();

        return Ok(items.Select(x => new
        {
            x.Id,
            x.Name,
            x.level,
            x.TotalMinutes,
            x.totalHours,
            x.Price,
            x.IsActive,
            pedagogicalTrack = BuildPedagogicalTrack(x.level, x.TotalMinutes)
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertCourseRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var validation = await ValidateCourseRequest(request, schoolId);
        if (validation is IActionResult error)
        {
            return error;
        }

        var course = new Course
        {
            SchoolId = schoolId,
            Name = request.Name.Trim(),
            Level = request.Level,
            TotalMinutes = ToMinutes(request.TotalHours),
            Price = request.Price,
            IsActive = true
        };

        _dbContext.Courses.Add(course);
        await _dbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { id = course.Id }, new { courseId = course.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCourseRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var validation = await ValidateCourseRequest(request, schoolId, id);
        if (validation is IActionResult error)
        {
            return error;
        }

        var course = await _dbContext.Courses.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
        if (course is null)
        {
            return NotFound();
        }

        course.Name = request.Name.Trim();
        course.Level = request.Level;
        course.TotalMinutes = ToMinutes(request.TotalHours);
        course.Price = request.Price;
        course.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync();
        return Ok();
    }

    private async Task<IActionResult?> ValidateCourseRequest(
        UpsertCourseContract request,
        Guid schoolId,
        Guid? currentCourseId = null)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest("O nome do curso é obrigatório.");
        }

        if (request.TotalHours <= 0)
        {
            return BadRequest("A carga horaria do curso precisa ser maior que zero.");
        }

        if (request.Price < 0)
        {
            return BadRequest("O preço do curso não pode ser negativo.");
        }

        var duplicateLevel = await _dbContext.Courses.AnyAsync(x =>
            x.SchoolId == schoolId &&
            x.Level == request.Level &&
            (!currentCourseId.HasValue || x.Id != currentCourseId.Value));

        if (duplicateLevel)
        {
            return Conflict("Ja existe um curso cadastrado para esse nivel.");
        }

        return null;
    }

    private static int ToMinutes(decimal totalHours)
        => (int)Math.Round(totalHours * 60m, MidpointRounding.AwayFromZero);

    private static object[] BuildPedagogicalTrack(string level, int totalMinutes)
    {
        var totalHours = Math.Max(1, (int)Math.Ceiling(totalMinutes / 60m));
        var modules = new[]
        {
            new { title = "Base técnica", weight = 0.25m, focus = "Segurança, vento e montagem" },
            new { title = "Controle do kite", weight = 0.25m, focus = "Janela de vento e controle de potência" },
            new { title = "Prancha e navegação", weight = 0.30m, focus = "Saída d'água, bordos e retorno" },
            new { title = "Autonomia", weight = 0.20m, focus = "Leitura de condição e rotina independente" }
        };

        return modules.Select((module, index) => new
        {
            id = $"{level}-{index + 1}",
            module.title,
            module.focus,
            estimatedHours = Math.Max(1, (int)Math.Round(totalHours * module.weight, MidpointRounding.AwayFromZero))
        }).ToArray<object>();
    }

    public interface UpsertCourseContract
    {
        string Name { get; }
        CourseLevel Level { get; }
        decimal TotalHours { get; }
        decimal Price { get; }
    }

    public sealed record UpsertCourseRequest(string Name, CourseLevel Level, decimal TotalHours, decimal Price)
        : UpsertCourseContract;

    public sealed record UpdateCourseRequest(string Name, CourseLevel Level, decimal TotalHours, decimal Price, bool IsActive)
        : UpsertCourseContract;
}
