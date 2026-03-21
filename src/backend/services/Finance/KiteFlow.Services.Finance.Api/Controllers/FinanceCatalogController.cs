using KiteFlow.BuildingBlocks.MultiTenancy;
using KiteFlow.Services.Finance.Api.Data;
using KiteFlow.Services.Finance.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Finance.Api.Controllers;

[ApiController]
[Authorize(Policy = "FinanceAccess")]
[Route("api/v1/finance")]
public sealed class FinanceCatalogController : ControllerBase
{
    private readonly FinanceDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;

    public FinanceCatalogController(FinanceDbContext dbContext, ICurrentTenant currentTenant)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories([FromQuery] FinancialCategoryDirection? direction)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var query = _dbContext.FinancialCategories.Where(x => x.SchoolId == schoolId);
        if (direction.HasValue)
        {
            var expected = direction.Value;
            query = query.Where(x => x.Direction == expected || x.Direction == FinancialCategoryDirection.Both);
        }

        var items = await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Direction,
                x.IsActive,
                x.SortOrder
            })
            .ToListAsync();

        return Ok(items.Select(x => new
        {
            x.Id,
            x.Name,
            direction = x.Direction.ToString(),
            directionCode = (int)x.Direction,
            x.IsActive,
            x.SortOrder
        }));
    }

    [HttpPost("categories")]
    public async Task<IActionResult> UpsertCategory([FromBody] UpsertCategoryRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("O nome da categoria é obrigatório.");
        }

        var normalizedName = request.Name.Trim();
        var category = request.Id.HasValue
            ? await _dbContext.FinancialCategories.FirstOrDefaultAsync(x => x.Id == request.Id.Value && x.SchoolId == schoolId)
            : null;

        if (category is null)
        {
            category = new FinancialCategory
            {
                SchoolId = schoolId
            };
            _dbContext.FinancialCategories.Add(category);
        }

        category.Name = normalizedName;
        category.Direction = request.Direction;
        category.IsActive = request.IsActive;
        category.SortOrder = request.SortOrder;

        await _dbContext.SaveChangesAsync();
        return Ok(new { categoryId = category.Id });
    }

    [HttpGet("cost-centers")]
    public async Task<IActionResult> GetCostCenters()
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var items = await _dbContext.CostCenters
            .Where(x => x.SchoolId == schoolId)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                x.IsActive
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost("cost-centers")]
    public async Task<IActionResult> UpsertCostCenter([FromBody] UpsertCostCenterRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("O nome do centro de custo é obrigatório.");
        }

        var costCenter = request.Id.HasValue
            ? await _dbContext.CostCenters.FirstOrDefaultAsync(x => x.Id == request.Id.Value && x.SchoolId == schoolId)
            : null;

        if (costCenter is null)
        {
            costCenter = new CostCenter
            {
                SchoolId = schoolId
            };
            _dbContext.CostCenters.Add(costCenter);
        }

        costCenter.Name = request.Name.Trim();
        costCenter.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        costCenter.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync();
        return Ok(new { costCenterId = costCenter.Id });
    }

    public sealed record UpsertCategoryRequest(
        Guid? Id,
        string Name,
        FinancialCategoryDirection Direction,
        bool IsActive,
        int SortOrder);

    public sealed record UpsertCostCenterRequest(
        Guid? Id,
        string Name,
        string? Description,
        bool IsActive);
}
