using KiteFlow.BuildingBlocks.MultiTenancy;
using KiteFlow.Services.Academics.Api.Data;
using KiteFlow.Services.Academics.Api.Domain;
using KiteFlow.Services.Academics.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Academics.Api.Controllers;

[ApiController]
[Authorize(Policy = "SchoolManagementAccess")]
[Route("api/v1/course-level-settings")]
public sealed class CourseLevelSettingsController : ControllerBase
{
    private readonly AcademicsDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;

    public CourseLevelSettingsController(AcademicsDbContext dbContext, ICurrentTenant currentTenant)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;
        var items = await CourseLevelCatalogDefaults.EnsureDefaultsAsync(_dbContext, schoolId);

        return Ok(new
        {
            availableLevels = CourseLevelCatalogDefaults.GetLevelCatalogOptions().Select(x => new
            {
                x.LevelValue,
                x.Name,
                x.SortOrder
            }),
            items = items.Select(x => new
            {
                x.Id,
                x.LevelValue,
                x.Name,
                x.IsActive,
                x.SortOrder,
                pedagogicalTrack = CourseLevelCatalogDefaults.DeserializeTrack(x.PedagogicalTrackJson)
            })
        });
    }

    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] UpsertCourseLevelSettingRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;
        await CourseLevelCatalogDefaults.EnsureDefaultsAsync(_dbContext, schoolId);

        if (request.LevelValue is < 2 or > 4)
        {
            return BadRequest("Nível inválido.");
        }

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest("O nome do nível é obrigatório.");
        }

        var track = request.PedagogicalTrack
            .Where(x => !string.IsNullOrWhiteSpace(x.Title))
            .Select((x, index) => new CourseLevelCatalogDefaults.PedagogicalTrackTemplateItem(
                string.IsNullOrWhiteSpace(x.Id) ? $"{request.LevelValue}-{index + 1}" : x.Id,
                x.Title.Trim(),
                (x.Focus ?? string.Empty).Trim(),
                Math.Clamp(x.WeightPercent, 1, 100)))
            .ToList();

        if (track.Count == 0)
        {
            return BadRequest("Cadastre pelo menos um módulo na trilha pedagógica.");
        }

        var totalWeight = track.Sum(x => x.WeightPercent);
        if (totalWeight != 100)
        {
            return BadRequest("A soma dos percentuais da trilha pedagógica deve ser 100%.");
        }

        Guid? settingId = null;
        if (!string.IsNullOrWhiteSpace(request.Id))
        {
            if (!Guid.TryParse(request.Id, out var parsedSettingId))
            {
                return BadRequest("Identificador da trilha pedagógica inválido.");
            }

            settingId = parsedSettingId;
        }

        var setting = settingId.HasValue
            ? await _dbContext.CourseLevelSettings
                .FirstOrDefaultAsync(x => x.SchoolId == schoolId && x.Id == settingId.Value)
            : null;
        var isNewSetting = setting is null;

        if (setting is null)
        {
            setting = new CourseLevelSetting
            {
                SchoolId = schoolId,
                LevelValue = request.LevelValue
            };
            _dbContext.CourseLevelSettings.Add(setting);
        }

        setting.LevelValue = request.LevelValue;
        setting.Name = name;
        setting.IsActive = request.IsActive;
        setting.SortOrder = isNewSetting
            ? await GetNextSortOrderAsync(schoolId)
            : setting.SortOrder;
        setting.PedagogicalTrackJson = CourseLevelCatalogDefaults.SerializeTrack(track);

        await _dbContext.SaveChangesAsync();

        return Ok(new { settingId = setting.Id, updatedAtUtc = DateTime.UtcNow });
    }

    [HttpDelete("{settingId:guid}")]
    public async Task<IActionResult> Delete(Guid settingId)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var setting = await _dbContext.CourseLevelSettings
            .FirstOrDefaultAsync(x => x.SchoolId == schoolId && x.Id == settingId);

        if (setting is null)
        {
            return NotFound();
        }

        var hasActiveCourses = await _dbContext.Courses.AnyAsync(x =>
            x.SchoolId == schoolId &&
            x.IsActive &&
            (
                x.CourseLevelSettingId == setting.Id ||
                (x.CourseLevelSettingId == null && (int)x.Level == setting.LevelValue)
            ));

        if (hasActiveCourses)
        {
            return Conflict("Esta trilha está em uso por curso ativo e não pode ser excluída.");
        }

        _dbContext.CourseLevelSettings.Remove(setting);
        await _dbContext.SaveChangesAsync();
        return Ok(new { deleted = true, settingId = setting.Id });
    }

    public sealed record UpsertCourseLevelSettingRequest(
        string? Id,
        int LevelValue,
        string Name,
        bool IsActive,
        IReadOnlyList<PedagogicalTrackItemRequest> PedagogicalTrack);

    public sealed record PedagogicalTrackItemRequest(
        string? Id,
        string Title,
        string? Focus,
        decimal WeightPercent);

    private async Task<int> GetNextSortOrderAsync(Guid schoolId)
    {
        var lastSortOrder = await _dbContext.CourseLevelSettings
            .Where(x => x.SchoolId == schoolId)
            .Select(x => (int?)x.SortOrder)
            .MaxAsync();

        return (lastSortOrder ?? 0) + 1;
    }
}
