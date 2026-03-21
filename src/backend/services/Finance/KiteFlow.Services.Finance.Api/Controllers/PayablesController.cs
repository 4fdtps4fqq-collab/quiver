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
public sealed class PayablesController : ControllerBase
{
    private readonly FinanceDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;

    public PayablesController(FinanceDbContext dbContext, ICurrentTenant currentTenant)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
    }

    [HttpGet("payables")]
    public async Task<IActionResult> GetPayables(
        [FromQuery] DateTime? fromDueUtc,
        [FromQuery] DateTime? toDueUtc,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? costCenterId,
        [FromQuery] bool? reconciled,
        [FromQuery] bool includeSettled = true)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var query = _dbContext.AccountsPayableEntries.Where(x => x.SchoolId == schoolId);

        if (fromDueUtc.HasValue)
        {
            query = query.Where(x => x.DueAtUtc >= fromDueUtc.Value);
        }

        if (toDueUtc.HasValue)
        {
            query = query.Where(x => x.DueAtUtc <= toDueUtc.Value);
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

        if (!includeSettled)
        {
            query = query.Where(x => x.Status != PayableStatus.Paid && x.Status != PayableStatus.Cancelled);
        }

        var now = DateTime.UtcNow;

        var paymentCounts = await _dbContext.AccountsPayablePayments
            .Where(x => x.SchoolId == schoolId)
            .GroupBy(x => x.PayableId)
            .Select(group => new { payableId = group.Key, count = group.Count() })
            .ToDictionaryAsync(x => x.payableId, x => x.count);

        var items = await query
            .OrderBy(x => x.Status == PayableStatus.Paid || x.Status == PayableStatus.Cancelled ? 1 : 0)
            .ThenBy(x => x.DueAtUtc)
            .ThenBy(x => x.Description)
            .Select(x => new
            {
                x.Id,
                x.Description,
                x.Notes,
                x.Vendor,
                x.CategoryId,
                x.CategoryName,
                x.CostCenterId,
                x.CostCenterName,
                x.Amount,
                x.PaidAmount,
                remainingAmount = x.Amount - x.PaidAmount,
                x.DueAtUtc,
                x.LastPaymentAtUtc,
                status = x.Status.ToString(),
                isOverdue = x.Status != PayableStatus.Paid &&
                            x.Status != PayableStatus.Cancelled &&
                            x.Amount > x.PaidAmount &&
                            x.DueAtUtc < now,
                x.ReconciledAtUtc,
                x.ReconciliationNote,
                x.CreatedAtUtc
            })
            .ToListAsync();

        return Ok(items.Select(x => new
        {
            x.Id,
            x.Description,
            x.Notes,
            x.Vendor,
            x.CategoryId,
            x.CategoryName,
            x.CostCenterId,
            x.CostCenterName,
            x.Amount,
            x.PaidAmount,
            x.remainingAmount,
            x.DueAtUtc,
            x.LastPaymentAtUtc,
            x.status,
            x.isOverdue,
            paymentsCount = paymentCounts.TryGetValue(x.Id, out var count) ? count : 0,
            x.ReconciledAtUtc,
            x.ReconciliationNote,
            x.CreatedAtUtc
        }));
    }

    [HttpPost("payables")]
    public async Task<IActionResult> CreatePayable([FromBody] UpsertPayableRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        if (ValidateRequest(request) is IActionResult error)
        {
            return error;
        }

        var category = await ResolveCategoryAsync(schoolId, request.CategoryId);
        var costCenter = await ResolveCostCenterAsync(schoolId, request.CostCenterId);

        var entry = new AccountsPayableEntry
        {
            SchoolId = schoolId,
            Description = request.Description.Trim(),
            Notes = NormalizeNullable(request.Notes),
            Vendor = NormalizeNullable(request.Vendor),
            CategoryId = category?.Id,
            CategoryName = category?.Name ?? NormalizeNullable(request.CategoryName),
            CostCenterId = costCenter?.Id,
            CostCenterName = costCenter?.Name,
            Amount = request.Amount,
            PaidAmount = 0m,
            DueAtUtc = request.DueAtUtc,
            Status = PayableStatus.Open
        };

        _dbContext.AccountsPayableEntries.Add(entry);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPayables), new { id = entry.Id }, new { payableId = entry.Id });
    }

    [HttpPut("payables/{id:guid}")]
    public async Task<IActionResult> UpdatePayable(Guid id, [FromBody] UpsertPayableRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        if (ValidateRequest(request) is IActionResult error)
        {
            return error;
        }

        var entry = await _dbContext.AccountsPayableEntries.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
        if (entry is null)
        {
            return NotFound();
        }

        var category = await ResolveCategoryAsync(schoolId, request.CategoryId);
        var costCenter = await ResolveCostCenterAsync(schoolId, request.CostCenterId);

        entry.Description = request.Description.Trim();
        entry.Notes = NormalizeNullable(request.Notes);
        entry.Vendor = NormalizeNullable(request.Vendor);
        entry.CategoryId = category?.Id;
        entry.CategoryName = category?.Name ?? NormalizeNullable(request.CategoryName);
        entry.CostCenterId = costCenter?.Id;
        entry.CostCenterName = costCenter?.Name;
        entry.Amount = request.Amount;
        entry.DueAtUtc = request.DueAtUtc;
        ApplyStatus(entry, entry.Status == PayableStatus.Cancelled);

        await _dbContext.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("payables/{id:guid}/payments")]
    public async Task<IActionResult> RegisterPayment(Guid id, [FromBody] RegisterPayablePaymentRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        if (request.Amount <= 0)
        {
            return BadRequest("O valor do pagamento deve ser maior que zero.");
        }

        var entry = await _dbContext.AccountsPayableEntries.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
        if (entry is null)
        {
            return NotFound();
        }

        if (entry.Status == PayableStatus.Cancelled)
        {
            return BadRequest("Não é possível pagar uma conta cancelada.");
        }

        var remainingAmount = entry.Amount - entry.PaidAmount;
        if (request.Amount > remainingAmount)
        {
            return BadRequest("O pagamento não pode ultrapassar o saldo em aberto da conta.");
        }

        var payment = new AccountsPayablePayment
        {
            SchoolId = schoolId,
            PayableId = entry.Id,
            Amount = request.Amount,
            PaidAtUtc = request.PaidAtUtc,
            Note = NormalizeNullable(request.Note)
        };

        entry.PaidAmount += request.Amount;
        entry.LastPaymentAtUtc = request.PaidAtUtc;
        ApplyStatus(entry, false);

        _dbContext.AccountsPayablePayments.Add(payment);
        _dbContext.ExpenseEntries.Add(new ExpenseEntry
        {
            SchoolId = schoolId,
            SourceType = "AccountsPayablePayment",
            SourceId = payment.Id,
            Category = ExpenseCategory.Other,
            CategoryId = entry.CategoryId,
            CategoryName = entry.CategoryName,
            CostCenterId = entry.CostCenterId,
            CostCenterName = entry.CostCenterName,
            Amount = request.Amount,
            Description = $"Pagamento de conta: {entry.Description}",
            Vendor = entry.Vendor,
            OccurredAtUtc = request.PaidAtUtc
        });

        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            paymentId = payment.Id,
            payableId = entry.Id,
            paidAmount = entry.PaidAmount,
            remainingAmount = entry.Amount - entry.PaidAmount,
            status = entry.Status.ToString()
        });
    }

    [HttpDelete("payables/{id:guid}")]
    public async Task<IActionResult> DeletePayable(Guid id)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var entry = await _dbContext.AccountsPayableEntries.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
        if (entry is null)
        {
            return NotFound();
        }

        var hasPayments = await _dbContext.AccountsPayablePayments.AnyAsync(x => x.SchoolId == schoolId && x.PayableId == id);
        if (hasPayments)
        {
            return BadRequest("Não é possível excluir uma conta que já possui pagamentos registrados.");
        }

        _dbContext.AccountsPayableEntries.Remove(entry);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    private IActionResult? ValidateRequest(UpsertPayableRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest("A descrição da conta a pagar é obrigatória.");
        }

        if (request.Amount <= 0)
        {
            return BadRequest("O valor da conta a pagar deve ser maior que zero.");
        }

        return null;
    }

    private async Task<FinancialCategory?> ResolveCategoryAsync(Guid schoolId, Guid? categoryId)
    {
        if (!categoryId.HasValue)
        {
            return null;
        }

        return await _dbContext.FinancialCategories.FirstOrDefaultAsync(x => x.Id == categoryId.Value && x.SchoolId == schoolId && x.IsActive);
    }

    private async Task<CostCenter?> ResolveCostCenterAsync(Guid schoolId, Guid? costCenterId)
    {
        if (!costCenterId.HasValue)
        {
            return null;
        }

        return await _dbContext.CostCenters.FirstOrDefaultAsync(x => x.Id == costCenterId.Value && x.SchoolId == schoolId && x.IsActive);
    }

    private static void ApplyStatus(AccountsPayableEntry entry, bool cancelled)
    {
        if (cancelled)
        {
            entry.Status = PayableStatus.Cancelled;
            return;
        }

        if (entry.PaidAmount <= 0)
        {
            entry.Status = PayableStatus.Open;
            return;
        }

        if (entry.PaidAmount >= entry.Amount)
        {
            entry.Status = PayableStatus.Paid;
            return;
        }

        entry.Status = PayableStatus.PartiallyPaid;
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public sealed record UpsertPayableRequest(
        Guid? CategoryId,
        string? CategoryName,
        Guid? CostCenterId,
        decimal Amount,
        DateTime DueAtUtc,
        string Description,
        string? Notes,
        string? Vendor);

    public sealed record RegisterPayablePaymentRequest(
        decimal Amount,
        DateTime PaidAtUtc,
        string? Note);
}
