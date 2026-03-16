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
public sealed class FinanceEntriesController : ControllerBase
{
    private readonly FinanceDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;

    public FinanceEntriesController(FinanceDbContext dbContext, ICurrentTenant currentTenant)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
    }

    [HttpGet("revenues")]
    public async Task<IActionResult> GetRevenues([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            return BadRequest("A data inicial deve ser menor ou igual a data final.");
        }

        var query = _dbContext.RevenueEntries.Where(x => x.SchoolId == schoolId);

        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.RecognizedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x => x.RecognizedAtUtc <= toUtc.Value);
        }

        var items = await query
            .OrderByDescending(x => x.RecognizedAtUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                sourceType = x.SourceType.ToString(),
                sourceTypeCode = (int)x.SourceType,
                sourceId = x.SourceId == Guid.Empty ? (Guid?)null : x.SourceId,
                x.Category,
                x.Amount,
                x.Description,
                x.RecognizedAtUtc,
                x.CreatedAtUtc
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost("revenues")]
    public async Task<IActionResult> CreateRevenue([FromBody] UpsertRevenueRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        if (ValidateRevenueRequest(request) is IActionResult error)
        {
            return error;
        }

        var entry = new RevenueEntry
        {
            SchoolId = schoolId,
            SourceType = request.SourceType,
            SourceId = request.SourceId ?? Guid.Empty,
            Category = request.Category.Trim(),
            Amount = request.Amount,
            RecognizedAtUtc = request.RecognizedAtUtc,
            Description = request.Description.Trim()
        };

        _dbContext.RevenueEntries.Add(entry);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetRevenues), new { id = entry.Id }, new { revenueId = entry.Id });
    }

    [HttpPut("revenues/{id:guid}")]
    public async Task<IActionResult> UpdateRevenue(Guid id, [FromBody] UpsertRevenueRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        if (ValidateRevenueRequest(request) is IActionResult error)
        {
            return error;
        }

        var entry = await _dbContext.RevenueEntries.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
        if (entry is null)
        {
            return NotFound();
        }

        entry.SourceType = request.SourceType;
        entry.SourceId = request.SourceId ?? Guid.Empty;
        entry.Category = request.Category.Trim();
        entry.Amount = request.Amount;
        entry.RecognizedAtUtc = request.RecognizedAtUtc;
        entry.Description = request.Description.Trim();

        await _dbContext.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("revenues/{id:guid}")]
    public async Task<IActionResult> DeleteRevenue(Guid id)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var entry = await _dbContext.RevenueEntries.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
        if (entry is null)
        {
            return NotFound();
        }

        _dbContext.RevenueEntries.Remove(entry);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("expenses")]
    public async Task<IActionResult> GetExpenses([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            return BadRequest("A data inicial deve ser menor ou igual a data final.");
        }

        var query = _dbContext.ExpenseEntries.Where(x => x.SchoolId == schoolId);

        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.OccurredAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x => x.OccurredAtUtc <= toUtc.Value);
        }

        var items = await query
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                category = x.Category.ToString(),
                categoryCode = (int)x.Category,
                x.Amount,
                x.Description,
                x.Vendor,
                x.OccurredAtUtc,
                x.CreatedAtUtc
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost("expenses")]
    public async Task<IActionResult> CreateExpense([FromBody] UpsertExpenseRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        if (ValidateExpenseRequest(request) is IActionResult error)
        {
            return error;
        }

        var entry = new ExpenseEntry
        {
            SchoolId = schoolId,
            Category = request.Category,
            Amount = request.Amount,
            Description = request.Description.Trim(),
            Vendor = string.IsNullOrWhiteSpace(request.Vendor) ? null : request.Vendor.Trim(),
            OccurredAtUtc = request.OccurredAtUtc
        };

        _dbContext.ExpenseEntries.Add(entry);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetExpenses), new { id = entry.Id }, new { expenseId = entry.Id });
    }

    [HttpPut("expenses/{id:guid}")]
    public async Task<IActionResult> UpdateExpense(Guid id, [FromBody] UpsertExpenseRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        if (ValidateExpenseRequest(request) is IActionResult error)
        {
            return error;
        }

        var entry = await _dbContext.ExpenseEntries.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
        if (entry is null)
        {
            return NotFound();
        }

        entry.Category = request.Category;
        entry.Amount = request.Amount;
        entry.Description = request.Description.Trim();
        entry.Vendor = string.IsNullOrWhiteSpace(request.Vendor) ? null : request.Vendor.Trim();
        entry.OccurredAtUtc = request.OccurredAtUtc;

        await _dbContext.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("expenses/{id:guid}")]
    public async Task<IActionResult> DeleteExpense(Guid id)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var entry = await _dbContext.ExpenseEntries.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
        if (entry is null)
        {
            return NotFound();
        }

        _dbContext.ExpenseEntries.Remove(entry);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    private IActionResult? ValidateRevenueRequest(UpsertRevenueRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Category))
        {
            return BadRequest("A categoria da receita é obrigatória.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest("A descrição da receita é obrigatória.");
        }

        if (request.Amount <= 0)
        {
            return BadRequest("O valor da receita deve ser maior que zero.");
        }

        return null;
    }

    private IActionResult? ValidateExpenseRequest(UpsertExpenseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest("A descrição da despesa é obrigatória.");
        }

        if (request.Amount <= 0)
        {
            return BadRequest("O valor da despesa deve ser maior que zero.");
        }

        return null;
    }

    public sealed record UpsertRevenueRequest(
        RevenueSourceType SourceType,
        Guid? SourceId,
        string Category,
        decimal Amount,
        DateTime RecognizedAtUtc,
        string Description);

    public sealed record UpsertExpenseRequest(
        ExpenseCategory Category,
        decimal Amount,
        DateTime OccurredAtUtc,
        string Description,
        string? Vendor);
}
