using KiteFlow.BuildingBlocks.MultiTenancy;
using KiteFlow.Services.Academics.Api.Data;
using KiteFlow.Services.Academics.Api.Domain;
using KiteFlow.Services.Academics.Api.Services;
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
        var levelSettings = await CourseLevelCatalogDefaults.EnsureDefaultsAsync(_dbContext, schoolId);
        var levelLookup = levelSettings
            .GroupBy(x => x.LevelValue)
            .ToDictionary(x => x.Key, x => x.OrderBy(item => item.SortOrder).First());
        var trackLookup = levelSettings.ToDictionary(x => x.Id);

        var items = await _dbContext.Courses
            .Where(x => x.SchoolId == schoolId)
            .Select(x => new
            {
                x.Id,
                x.Name,
                levelValue = (int)x.Level,
                x.CourseLevelSettingId,
                x.TotalMinutes,
                totalHours = Math.Round(x.TotalMinutes / 60m, 2),
                x.Price,
                x.IsActive
            })
            .ToListAsync();

        return Ok(items
            .OrderBy(x => levelLookup.TryGetValue(x.levelValue, out var setting) ? setting.SortOrder : x.levelValue)
            .Select(x => new
        {
            x.Id,
            x.Name,
            level = CourseLevelCatalogDefaults.TranslateLevelName(x.levelValue),
            x.levelValue,
            trackTemplateId = x.CourseLevelSettingId,
            trackTemplateName = x.CourseLevelSettingId.HasValue && trackLookup.TryGetValue(x.CourseLevelSettingId.Value, out var selectedTrack)
                ? selectedTrack.Name
                : levelLookup.TryGetValue(x.levelValue, out var defaultTrack)
                    ? defaultTrack.Name
                    : "Trilha padrão",
            x.TotalMinutes,
            x.totalHours,
            x.Price,
            x.IsActive,
            pedagogicalTrack = CourseLevelCatalogDefaults.BuildPedagogicalTrack(
                x.CourseLevelSettingId.HasValue && trackLookup.TryGetValue(x.CourseLevelSettingId.Value, out var configuredTrack)
                    ? configuredTrack
                    : levelLookup.TryGetValue(x.levelValue, out var fallbackTrack) ? fallbackTrack : null,
                x.TotalMinutes)
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
            CourseLevelSettingId = request.TrackTemplateId,
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
        course.CourseLevelSettingId = request.TrackTemplateId;
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

        var levelSettings = await CourseLevelCatalogDefaults.EnsureDefaultsAsync(_dbContext, schoolId);
        var selectedTrack = await _dbContext.CourseLevelSettings
            .FirstOrDefaultAsync(x =>
                x.SchoolId == schoolId &&
                x.Id == request.TrackTemplateId);

        if (selectedTrack is null)
        {
            return BadRequest("Selecione uma trilha pedagógica válida.");
        }

        if (!selectedTrack.IsActive)
        {
            return BadRequest("Selecione uma trilha pedagógica ativa.");
        }

        if (selectedTrack.LevelValue != (int)request.Level)
        {
            return BadRequest("A trilha pedagógica precisa pertencer ao mesmo nível do curso.");
        }

        if (!levelSettings.Any(x => x.LevelValue == (int)request.Level && x.IsActive))
        {
            return BadRequest("Selecione um nível de curso ativo na trilha pedagógica.");
        }

        return null;
    }

    private static int ToMinutes(decimal totalHours)
        => (int)Math.Round(totalHours * 60m, MidpointRounding.AwayFromZero);

    public interface UpsertCourseContract
    {
        string Name { get; }
        CourseLevel Level { get; }
        Guid TrackTemplateId { get; }
        decimal TotalHours { get; }
        decimal Price { get; }
    }

    public sealed record UpsertCourseRequest(string Name, CourseLevel Level, Guid TrackTemplateId, decimal TotalHours, decimal Price)
        : UpsertCourseContract;

    public sealed record UpdateCourseRequest(string Name, CourseLevel Level, Guid TrackTemplateId, decimal TotalHours, decimal Price, bool IsActive)
        : UpsertCourseContract;
}
