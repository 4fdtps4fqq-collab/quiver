using KiteFlow.BuildingBlocks.MultiTenancy;
using KiteFlow.Services.Finance.Api.Data;
using KiteFlow.Services.Finance.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;

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
    public async Task<IActionResult> GetRevenues(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? costCenterId,
        [FromQuery] bool? reconciled)
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

        if (categoryId.HasValue)
        {
            query = query.Where(x => x.CategoryId == categoryId.Value);
        }

        if (costCenterId.HasValue)
        {
            query = query.Where(x => x.CostCenterId == costCenterId.Value);
        }

        if (reconciled.HasValue)
        {
            query = reconciled.Value
                ? query.Where(x => x.ReconciledAtUtc != null)
                : query.Where(x => x.ReconciledAtUtc == null);
        }

        var items = await query
            .OrderByDescending(x => x.RecognizedAtUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.SourceType,
                sourceId = x.SourceId == Guid.Empty ? (Guid?)null : x.SourceId,
                x.CategoryId,
                x.Category,
                x.CostCenterId,
                x.CostCenterName,
                x.Amount,
                x.Description,
                x.RecognizedAtUtc,
                x.ReconciledAtUtc,
                x.ReconciliationNote,
                x.CreatedAtUtc
            })
            .ToListAsync();

        return Ok(items.Select(x => new
        {
            x.Id,
            sourceType = x.SourceType.ToString(),
            sourceTypeCode = (int)x.SourceType,
            x.sourceId,
            x.CategoryId,
            x.Category,
            x.CostCenterId,
            x.CostCenterName,
            x.Amount,
            x.Description,
            x.RecognizedAtUtc,
            x.ReconciledAtUtc,
            x.ReconciliationNote,
            x.CreatedAtUtc
        }));
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

        FinancialCategory? categorySnapshot;
        CostCenter? costCenterSnapshot;
        try
        {
            categorySnapshot = await ResolveCategorySnapshotAsync(schoolId, request.CategoryId, request.Category);
            costCenterSnapshot = await ResolveCostCenterSnapshotAsync(schoolId, request.CostCenterId);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }

        var entry = new RevenueEntry
        {
            SchoolId = schoolId,
            SourceType = request.SourceType,
            SourceId = request.SourceId ?? Guid.Empty,
            CategoryId = categorySnapshot?.Id,
            Category = categorySnapshot?.Name ?? request.Category.Trim(),
            CostCenterId = costCenterSnapshot?.Id,
            CostCenterName = costCenterSnapshot?.Name,
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

        FinancialCategory? categorySnapshot;
        CostCenter? costCenterSnapshot;
        try
        {
            categorySnapshot = await ResolveCategorySnapshotAsync(schoolId, request.CategoryId, request.Category);
            costCenterSnapshot = await ResolveCostCenterSnapshotAsync(schoolId, request.CostCenterId);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }

        entry.SourceType = request.SourceType;
        entry.SourceId = request.SourceId ?? Guid.Empty;
        entry.CategoryId = categorySnapshot?.Id;
        entry.Category = categorySnapshot?.Name ?? request.Category.Trim();
        entry.CostCenterId = costCenterSnapshot?.Id;
        entry.CostCenterName = costCenterSnapshot?.Name;
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
    public async Task<IActionResult> GetExpenses(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? costCenterId,
        [FromQuery] bool? reconciled)
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

        if (categoryId.HasValue)
        {
            query = query.Where(x => x.CategoryId == categoryId.Value);
        }

        if (costCenterId.HasValue)
        {
            query = query.Where(x => x.CostCenterId == costCenterId.Value);
        }

        if (reconciled.HasValue)
        {
            query = reconciled.Value
                ? query.Where(x => x.ReconciledAtUtc != null)
                : query.Where(x => x.ReconciledAtUtc == null);
        }

        var items = await query
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.Category,
                x.CategoryId,
                categoryName = x.CategoryName,
                x.CostCenterId,
                x.CostCenterName,
                x.Amount,
                x.Description,
                x.Vendor,
                x.OccurredAtUtc,
                x.ReconciledAtUtc,
                x.ReconciliationNote,
                x.CreatedAtUtc
            })
            .ToListAsync();

        return Ok(items.Select(x => new
        {
            x.Id,
            category = x.Category.ToString(),
            categoryCode = (int)x.Category,
            x.CategoryId,
            x.categoryName,
            x.CostCenterId,
            x.CostCenterName,
            x.Amount,
            x.Description,
            x.Vendor,
            x.OccurredAtUtc,
            x.ReconciledAtUtc,
            x.ReconciliationNote,
            x.CreatedAtUtc
        }));
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

        FinancialCategory? categorySnapshot;
        CostCenter? costCenterSnapshot;
        try
        {
            categorySnapshot = await ResolveCategorySnapshotAsync(schoolId, request.CategoryId, request.CategoryName);
            costCenterSnapshot = await ResolveCostCenterSnapshotAsync(schoolId, request.CostCenterId);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }

        var entry = new ExpenseEntry
        {
            SchoolId = schoolId,
            Category = request.Category,
            CategoryId = categorySnapshot?.Id,
            CategoryName = categorySnapshot?.Name ?? NormalizeNullable(request.CategoryName),
            CostCenterId = costCenterSnapshot?.Id,
            CostCenterName = costCenterSnapshot?.Name,
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

        FinancialCategory? categorySnapshot;
        CostCenter? costCenterSnapshot;
        try
        {
            categorySnapshot = await ResolveCategorySnapshotAsync(schoolId, request.CategoryId, request.CategoryName);
            costCenterSnapshot = await ResolveCostCenterSnapshotAsync(schoolId, request.CostCenterId);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }

        entry.Category = request.Category;
        entry.CategoryId = categorySnapshot?.Id;
        entry.CategoryName = categorySnapshot?.Name ?? NormalizeNullable(request.CategoryName);
        entry.CostCenterId = costCenterSnapshot?.Id;
        entry.CostCenterName = costCenterSnapshot?.Name;
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

    [HttpGet("exports/{kind}")]
    public async Task<IActionResult> Export(string kind, [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            return BadRequest("A data inicial deve ser menor ou igual a data final.");
        }

        var csv = kind.ToLowerInvariant() switch
        {
            "revenues" => await BuildRevenueExportAsync(schoolId, fromUtc, toUtc),
            "expenses" => await BuildExpenseExportAsync(schoolId, fromUtc, toUtc),
            "receivables" => await BuildReceivableExportAsync(schoolId, fromUtc, toUtc),
            "payables" => await BuildPayableExportAsync(schoolId, fromUtc, toUtc),
            _ => null
        };

        if (csv is null)
        {
            return NotFound();
        }

        return File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", $"finance-{kind}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    [HttpPost("reconciliation/{kind}/{id:guid}")]
    public async Task<IActionResult> ReconcileEntry(string kind, Guid id, [FromBody] ReconcileEntryRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        var now = request.ReconciledAtUtc ?? DateTime.UtcNow;

        switch (kind.ToLowerInvariant())
        {
            case "revenue":
            {
                var entry = await _dbContext.RevenueEntries.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
                if (entry is null)
                {
                    return NotFound();
                }

                entry.ReconciledAtUtc = now;
                entry.ReconciledByUserId = userId;
                entry.ReconciliationNote = NormalizeNullable(request.Note);

                _dbContext.FinancialReconciliationRecords.Add(new FinancialReconciliationRecord
                {
                    SchoolId = schoolId,
                    EntryKind = FinancialEntryKind.Revenue,
                    EntryId = entry.Id,
                    AmountSnapshot = entry.Amount,
                    ReconciledAtUtc = now,
                    ReconciledByUserId = userId,
                    Note = NormalizeNullable(request.Note)
                });
                break;
            }
            case "expense":
            {
                var entry = await _dbContext.ExpenseEntries.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
                if (entry is null)
                {
                    return NotFound();
                }

                entry.ReconciledAtUtc = now;
                entry.ReconciledByUserId = userId;
                entry.ReconciliationNote = NormalizeNullable(request.Note);

                _dbContext.FinancialReconciliationRecords.Add(new FinancialReconciliationRecord
                {
                    SchoolId = schoolId,
                    EntryKind = FinancialEntryKind.Expense,
                    EntryId = entry.Id,
                    AmountSnapshot = entry.Amount,
                    ReconciledAtUtc = now,
                    ReconciledByUserId = userId,
                    Note = NormalizeNullable(request.Note)
                });
                break;
            }
            case "receivable":
            {
                var entry = await _dbContext.AccountsReceivableEntries.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
                if (entry is null)
                {
                    return NotFound();
                }

                entry.ReconciledAtUtc = now;
                entry.ReconciledByUserId = userId;
                entry.ReconciliationNote = NormalizeNullable(request.Note);

                _dbContext.FinancialReconciliationRecords.Add(new FinancialReconciliationRecord
                {
                    SchoolId = schoolId,
                    EntryKind = FinancialEntryKind.Receivable,
                    EntryId = entry.Id,
                    AmountSnapshot = entry.Amount - entry.PaidAmount,
                    ReconciledAtUtc = now,
                    ReconciledByUserId = userId,
                    Note = NormalizeNullable(request.Note)
                });
                break;
            }
            case "payable":
            {
                var entry = await _dbContext.AccountsPayableEntries.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
                if (entry is null)
                {
                    return NotFound();
                }

                entry.ReconciledAtUtc = now;
                entry.ReconciledByUserId = userId;
                entry.ReconciliationNote = NormalizeNullable(request.Note);

                _dbContext.FinancialReconciliationRecords.Add(new FinancialReconciliationRecord
                {
                    SchoolId = schoolId,
                    EntryKind = FinancialEntryKind.Payable,
                    EntryId = entry.Id,
                    AmountSnapshot = entry.Amount - entry.PaidAmount,
                    ReconciledAtUtc = now,
                    ReconciledByUserId = userId,
                    Note = NormalizeNullable(request.Note)
                });
                break;
            }
            default:
                return NotFound();
        }

        await _dbContext.SaveChangesAsync();
        return Ok(new { reconciled = true, reconciledAtUtc = now });
    }

    [HttpDelete("reconciliation/{kind}/{id:guid}")]
    public async Task<IActionResult> UnreconcileEntry(string kind, Guid id)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        switch (kind.ToLowerInvariant())
        {
            case "revenue":
            {
                var entry = await _dbContext.RevenueEntries.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
                if (entry is null)
                {
                    return NotFound();
                }

                entry.ReconciledAtUtc = null;
                entry.ReconciledByUserId = null;
                entry.ReconciliationNote = null;
                break;
            }
            case "expense":
            {
                var entry = await _dbContext.ExpenseEntries.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
                if (entry is null)
                {
                    return NotFound();
                }

                entry.ReconciledAtUtc = null;
                entry.ReconciledByUserId = null;
                entry.ReconciliationNote = null;
                break;
            }
            case "receivable":
            {
                var entry = await _dbContext.AccountsReceivableEntries.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
                if (entry is null)
                {
                    return NotFound();
                }

                entry.ReconciledAtUtc = null;
                entry.ReconciledByUserId = null;
                entry.ReconciliationNote = null;
                break;
            }
            case "payable":
            {
                var entry = await _dbContext.AccountsPayableEntries.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
                if (entry is null)
                {
                    return NotFound();
                }

                entry.ReconciledAtUtc = null;
                entry.ReconciledByUserId = null;
                entry.ReconciliationNote = null;
                break;
            }
            default:
                return NotFound();
        }

        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    private IActionResult? ValidateRevenueRequest(UpsertRevenueRequest request)
    {
        if (!request.CategoryId.HasValue && string.IsNullOrWhiteSpace(request.Category))
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

    private async Task<FinancialCategory?> ResolveCategorySnapshotAsync(Guid schoolId, Guid? categoryId, string? fallbackName)
    {
        if (!categoryId.HasValue)
        {
            return null;
        }

        var category = await _dbContext.FinancialCategories.FirstOrDefaultAsync(x =>
            x.Id == categoryId.Value &&
            x.SchoolId == schoolId &&
            x.IsActive);

        if (category is null)
        {
            throw new InvalidOperationException("A categoria financeira selecionada não está disponível para esta escola.");
        }

        return category;
    }

    private async Task<CostCenter?> ResolveCostCenterSnapshotAsync(Guid schoolId, Guid? costCenterId)
    {
        if (!costCenterId.HasValue)
        {
            return null;
        }

        var costCenter = await _dbContext.CostCenters.FirstOrDefaultAsync(x =>
            x.Id == costCenterId.Value &&
            x.SchoolId == schoolId &&
            x.IsActive);

        if (costCenter is null)
        {
            throw new InvalidOperationException("O centro de custo selecionado não está disponível para esta escola.");
        }

        return costCenter;
    }

    private async Task<string> BuildRevenueExportAsync(Guid schoolId, DateTime? fromUtc, DateTime? toUtc)
    {
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
            .Select(x => new
            {
                x.Description,
                x.Category,
                x.CostCenterName,
                x.Amount,
                x.RecognizedAtUtc,
                x.ReconciledAtUtc
            })
            .ToListAsync();

        var builder = new StringBuilder();
        builder.AppendLine("Descricao,Categoria,CentroDeCusto,Valor,ReconhecidaEm,ConciliadaEm");
        foreach (var item in items)
        {
            builder.AppendLine($"{EscapeCsv(item.Description)},{EscapeCsv(item.Category)},{EscapeCsv(item.CostCenterName)},{item.Amount:0.00},{item.RecognizedAtUtc:O},{item.ReconciledAtUtc:O}");
        }

        return builder.ToString();
    }

    private async Task<string> BuildExpenseExportAsync(Guid schoolId, DateTime? fromUtc, DateTime? toUtc)
    {
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
            .Select(x => new
            {
                x.Description,
                x.CategoryName,
                x.Category,
                x.CostCenterName,
                x.Vendor,
                x.Amount,
                x.OccurredAtUtc,
                x.ReconciledAtUtc
            })
            .ToListAsync();

        var builder = new StringBuilder();
        builder.AppendLine("Descricao,Categoria,CentroDeCusto,Fornecedor,Valor,OcorridaEm,ConciliadaEm");
        foreach (var item in items)
        {
            var category = item.CategoryName ?? item.Category.ToString();
            builder.AppendLine($"{EscapeCsv(item.Description)},{EscapeCsv(category)},{EscapeCsv(item.CostCenterName)},{EscapeCsv(item.Vendor)},{item.Amount:0.00},{item.OccurredAtUtc:O},{item.ReconciledAtUtc:O}");
        }

        return builder.ToString();
    }

    private async Task<string> BuildReceivableExportAsync(Guid schoolId, DateTime? fromUtc, DateTime? toUtc)
    {
        var query = _dbContext.AccountsReceivableEntries.Where(x => x.SchoolId == schoolId);
        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.DueAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x => x.DueAtUtc <= toUtc.Value);
        }

        var items = await query
            .OrderByDescending(x => x.DueAtUtc)
            .Select(x => new
            {
                x.StudentNameSnapshot,
                x.Description,
                Category = x.CategoryName ?? string.Empty,
                x.CostCenterName,
                x.Amount,
                x.PaidAmount,
                x.DueAtUtc,
                x.ReconciledAtUtc,
                x.Status
            })
            .ToListAsync();

        var builder = new StringBuilder();
        builder.AppendLine("Aluno,Descricao,Categoria,CentroDeCusto,Valor,Pago,Vencimento,Situacao,ConciliadaEm");
        foreach (var item in items)
        {
            builder.AppendLine(
                $"{EscapeCsv(item.StudentNameSnapshot)},{EscapeCsv(item.Description)},{EscapeCsv(item.Category)},{EscapeCsv(item.CostCenterName)},{item.Amount:0.00},{item.PaidAmount:0.00},{item.DueAtUtc:O},{EscapeCsv(item.Status.ToString())},{item.ReconciledAtUtc:O}");
        }

        return builder.ToString();
    }

    private async Task<string> BuildPayableExportAsync(Guid schoolId, DateTime? fromUtc, DateTime? toUtc)
    {
        var query = _dbContext.AccountsPayableEntries.Where(x => x.SchoolId == schoolId);
        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.DueAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x => x.DueAtUtc <= toUtc.Value);
        }

        var items = await query
            .OrderByDescending(x => x.DueAtUtc)
            .Select(x => new
            {
                x.Description,
                x.Vendor,
                Category = x.CategoryName ?? string.Empty,
                x.CostCenterName,
                x.Amount,
                x.PaidAmount,
                x.DueAtUtc,
                x.ReconciledAtUtc,
                x.Status
            })
            .ToListAsync();

        var builder = new StringBuilder();
        builder.AppendLine("Descricao,Fornecedor,Categoria,CentroDeCusto,Valor,Pago,Vencimento,Situacao,ConciliadaEm");
        foreach (var item in items)
        {
            builder.AppendLine(
                $"{EscapeCsv(item.Description)},{EscapeCsv(item.Vendor)},{EscapeCsv(item.Category)},{EscapeCsv(item.CostCenterName)},{item.Amount:0.00},{item.PaidAmount:0.00},{item.DueAtUtc:O},{EscapeCsv(item.Status.ToString())},{item.ReconciledAtUtc:O}");
        }

        return builder.ToString();
    }

    private static string EscapeCsv(string? value)
        => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public sealed record UpsertRevenueRequest(
        RevenueSourceType SourceType,
        Guid? SourceId,
        Guid? CategoryId,
        string Category,
        Guid? CostCenterId,
        decimal Amount,
        DateTime RecognizedAtUtc,
        string Description);

    public sealed record UpsertExpenseRequest(
        ExpenseCategory Category,
        Guid? CategoryId,
        string? CategoryName,
        Guid? CostCenterId,
        decimal Amount,
        DateTime OccurredAtUtc,
        string Description,
        string? Vendor);

    public sealed record ReconcileEntryRequest(string? Note, DateTime? ReconciledAtUtc);
}
