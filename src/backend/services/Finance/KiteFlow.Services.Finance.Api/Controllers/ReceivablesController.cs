using KiteFlow.BuildingBlocks.MultiTenancy;
using KiteFlow.Services.Finance.Api.Data;
using KiteFlow.Services.Finance.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace KiteFlow.Services.Finance.Api.Controllers;

[ApiController]
[Authorize(Policy = "FinanceAccess")]
[Route("api/v1/finance")]
public sealed class ReceivablesController : ControllerBase
{
    private readonly FinanceDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;
    private readonly IConfiguration _configuration;

    public ReceivablesController(
        FinanceDbContext dbContext,
        ICurrentTenant currentTenant,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
        _configuration = configuration;
    }

    [HttpGet("receivables")]
    [Authorize(Policy = "FinanceAccess")]
    public async Task<IActionResult> GetReceivables(
        [FromQuery] DateTime? fromDueUtc,
        [FromQuery] DateTime? toDueUtc,
        [FromQuery] Guid? studentId,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? costCenterId,
        [FromQuery] bool? reconciled,
        [FromQuery] bool includeSettled = true)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        if (fromDueUtc.HasValue && toDueUtc.HasValue && fromDueUtc > toDueUtc)
        {
            return BadRequest("A data inicial deve ser menor ou igual à data final.");
        }

        var query = _dbContext.AccountsReceivableEntries.Where(x => x.SchoolId == schoolId);

        if (fromDueUtc.HasValue)
        {
            query = query.Where(x => x.DueAtUtc >= fromDueUtc.Value);
        }

        if (toDueUtc.HasValue)
        {
            query = query.Where(x => x.DueAtUtc <= toDueUtc.Value);
        }

        if (studentId.HasValue)
        {
            query = query.Where(x => x.StudentId == studentId.Value);
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
            query = query.Where(x => x.Status != ReceivableStatus.Paid && x.Status != ReceivableStatus.Cancelled);
        }

        var now = DateTime.UtcNow;
        var paymentCounts = await _dbContext.AccountsReceivablePayments
            .Where(x => x.SchoolId == schoolId)
            .GroupBy(x => x.ReceivableId)
            .Select(group => new
            {
                receivableId = group.Key,
                count = group.Count()
            })
            .ToDictionaryAsync(x => x.receivableId, x => x.count);

        var items = await query
            .OrderBy(x => x.Status == ReceivableStatus.Paid || x.Status == ReceivableStatus.Cancelled ? 1 : 0)
            .ThenBy(x => x.DueAtUtc)
            .ThenBy(x => x.StudentNameSnapshot)
            .Select(x => new
            {
                x.Id,
                x.StudentId,
                x.EnrollmentId,
                x.StudentNameSnapshot,
                x.Description,
                x.Notes,
                x.CategoryId,
                x.CategoryName,
                x.CostCenterId,
                x.CostCenterName,
                x.Amount,
                x.PaidAmount,
                remainingAmount = x.Amount - x.PaidAmount,
                x.DueAtUtc,
                x.LastPaymentAtUtc,
                x.ReconciledAtUtc,
                x.ReconciliationNote,
                x.Status,
                isOverdue = x.Status != ReceivableStatus.Paid &&
                            x.Status != ReceivableStatus.Cancelled &&
                            x.Amount > x.PaidAmount &&
                            x.DueAtUtc < now,
                x.CreatedAtUtc
            })
            .ToListAsync();

        return Ok(items.Select(x => new
        {
            x.Id,
            x.StudentId,
            x.EnrollmentId,
            x.StudentNameSnapshot,
            x.Description,
            x.Notes,
            x.CategoryId,
            x.CategoryName,
            x.CostCenterId,
            x.CostCenterName,
            x.Amount,
            x.PaidAmount,
            x.remainingAmount,
            x.DueAtUtc,
            x.LastPaymentAtUtc,
            x.ReconciledAtUtc,
            x.ReconciliationNote,
            status = x.Status.ToString(),
            x.isOverdue,
            paymentsCount = paymentCounts.TryGetValue(x.Id, out var count) ? count : 0,
            x.CreatedAtUtc
        }));
    }

    [HttpPost("receivables")]
    [Authorize(Policy = "FinanceAccess")]
    public async Task<IActionResult> CreateReceivable([FromBody] UpsertReceivableRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        if (ValidateReceivableRequest(request) is IActionResult error)
        {
            return error;
        }

        var category = await ResolveCategoryAsync(schoolId, request.CategoryId);
        var costCenter = await ResolveCostCenterAsync(schoolId, request.CostCenterId);

        var entry = new AccountsReceivableEntry
        {
            SchoolId = schoolId,
            StudentId = request.StudentId,
            EnrollmentId = request.EnrollmentId,
            StudentNameSnapshot = request.StudentNameSnapshot.Trim(),
            Description = request.Description.Trim(),
            Notes = NormalizeNullable(request.Notes),
            CategoryId = category?.Id,
            CategoryName = category?.Name ?? NormalizeNullable(request.CategoryName),
            CostCenterId = costCenter?.Id,
            CostCenterName = costCenter?.Name,
            Amount = request.Amount,
            PaidAmount = 0m,
            DueAtUtc = request.DueAtUtc,
            Status = ReceivableStatus.Open
        };

        _dbContext.AccountsReceivableEntries.Add(entry);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetReceivables), new { id = entry.Id }, new { receivableId = entry.Id });
    }

    [HttpPut("receivables/{id:guid}")]
    [Authorize(Policy = "FinanceAccess")]
    public async Task<IActionResult> UpdateReceivable(Guid id, [FromBody] UpsertReceivableRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        if (ValidateReceivableRequest(request) is IActionResult error)
        {
            return error;
        }

        var entry = await _dbContext.AccountsReceivableEntries.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
        if (entry is null)
        {
            return NotFound();
        }

        var category = await ResolveCategoryAsync(schoolId, request.CategoryId);
        var costCenter = await ResolveCostCenterAsync(schoolId, request.CostCenterId);

        entry.StudentId = request.StudentId;
        entry.EnrollmentId = request.EnrollmentId;
        entry.StudentNameSnapshot = request.StudentNameSnapshot.Trim();
        entry.Description = request.Description.Trim();
        entry.Notes = NormalizeNullable(request.Notes);
        entry.CategoryId = category?.Id;
        entry.CategoryName = category?.Name ?? NormalizeNullable(request.CategoryName);
        entry.CostCenterId = costCenter?.Id;
        entry.CostCenterName = costCenter?.Name;
        entry.Amount = request.Amount;
        entry.DueAtUtc = request.DueAtUtc;
        ApplyReceivableStatus(entry, entry.Status == ReceivableStatus.Cancelled);

        await _dbContext.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("receivables/{id:guid}/payments")]
    [Authorize(Policy = "FinanceAccess")]
    public async Task<IActionResult> RegisterPayment(Guid id, [FromBody] RegisterReceivablePaymentRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        if (request.Amount <= 0)
        {
            return BadRequest("O valor do pagamento deve ser maior que zero.");
        }

        var entry = await _dbContext.AccountsReceivableEntries.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
        if (entry is null)
        {
            return NotFound();
        }

        if (entry.Status == ReceivableStatus.Cancelled)
        {
            return BadRequest("Não é possível receber uma cobrança cancelada.");
        }

        var remainingAmount = entry.Amount - entry.PaidAmount;
        if (request.Amount > remainingAmount)
        {
            return BadRequest("O pagamento não pode ultrapassar o saldo em aberto da cobrança.");
        }

        var payment = new AccountsReceivablePayment
        {
            SchoolId = schoolId,
            ReceivableId = entry.Id,
            Amount = request.Amount,
            PaidAtUtc = request.PaidAtUtc,
            Note = NormalizeNullable(request.Note)
        };

        entry.PaidAmount += request.Amount;
        entry.LastPaymentAtUtc = request.PaidAtUtc;
        ApplyReceivableStatus(entry, false);

        _dbContext.AccountsReceivablePayments.Add(payment);
        _dbContext.RevenueEntries.Add(new RevenueEntry
        {
            SchoolId = schoolId,
            SourceType = RevenueSourceType.ManualAdjustment,
            SourceId = entry.Id,
            CategoryId = entry.CategoryId,
            Category = entry.CategoryName ?? "Recebimento",
            CostCenterId = entry.CostCenterId,
            CostCenterName = entry.CostCenterName,
            Amount = request.Amount,
            RecognizedAtUtc = request.PaidAtUtc,
            Description = $"Recebimento de {entry.StudentNameSnapshot}: {entry.Description}"
        });

        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            paymentId = payment.Id,
            receivableId = entry.Id,
            paidAmount = entry.PaidAmount,
            remainingAmount = entry.Amount - entry.PaidAmount,
            status = entry.Status.ToString()
        });
    }

    [HttpDelete("receivables/{id:guid}")]
    [Authorize(Policy = "FinanceAccess")]
    public async Task<IActionResult> DeleteReceivable(Guid id)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var entry = await _dbContext.AccountsReceivableEntries.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
        if (entry is null)
        {
            return NotFound();
        }

        var hasPayments = await _dbContext.AccountsReceivablePayments.AnyAsync(x => x.SchoolId == schoolId && x.ReceivableId == id);
        if (hasPayments)
        {
            return BadRequest("Não é possível excluir uma cobrança que já possui pagamentos registrados.");
        }

        _dbContext.AccountsReceivableEntries.Remove(entry);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("students/financial-statuses")]
    [Authorize(Policy = "FinanceAccess")]
    public async Task<IActionResult> GetStudentFinancialStatuses([FromQuery] Guid? studentId)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;
        return Ok(BuildStudentFinancialStatusesEnvelope(await GetStudentFinancialStatusesItemsAsync(schoolId, studentId)));
    }

    [AllowAnonymous]
    [HttpGet("internal/students/financial-statuses")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> GetInternalStudentFinancialStatuses([FromQuery] Guid schoolId, [FromQuery] Guid? studentId)
    {
        if (!IsInternalGatewayCall())
        {
            return Unauthorized("Esta rota interna aceita apenas chamadas autenticadas entre serviços.");
        }

        if (schoolId == Guid.Empty)
        {
            return BadRequest("O identificador da escola é obrigatório.");
        }

        return Ok(BuildStudentFinancialStatusesEnvelope(await GetStudentFinancialStatusesItemsAsync(schoolId, studentId)));
    }

    private async Task<List<StudentFinancialStatusItem>> GetStudentFinancialStatusesItemsAsync(Guid schoolId, Guid? studentId)
    {
        var now = DateTime.UtcNow;

        var receivables = await _dbContext.AccountsReceivableEntries
            .Where(x => x.SchoolId == schoolId && x.Status != ReceivableStatus.Cancelled)
            .Where(x => !studentId.HasValue || x.StudentId == studentId.Value)
            .Select(x => new
            {
                x.StudentId,
                x.StudentNameSnapshot,
                x.Amount,
                x.PaidAmount,
                x.DueAtUtc,
                x.Status
            })
            .ToListAsync();

        return receivables
            .GroupBy(x => new { x.StudentId, x.StudentNameSnapshot })
            .Select(group =>
            {
                var openItems = group
                    .Where(x => x.Status != ReceivableStatus.Paid && x.Amount > x.PaidAmount)
                    .ToList();
                var overdueItems = openItems.Where(x => x.DueAtUtc < now).ToList();
                var openAmount = openItems.Sum(x => x.Amount - x.PaidAmount);
                var overdueAmount = overdueItems.Sum(x => x.Amount - x.PaidAmount);

                var status = overdueItems.Count > 0
                    ? "Delinquent"
                    : openItems.Count > 0
                        ? "DueSoon"
                        : "UpToDate";

                return new StudentFinancialStatusItem(
                    group.Key.StudentId,
                    group.Key.StudentNameSnapshot,
                    status,
                    openAmount,
                    overdueAmount,
                    openItems.Count,
                    overdueItems.Count,
                    openItems.OrderBy(x => x.DueAtUtc).Select(x => (DateTime?)x.DueAtUtc).FirstOrDefault());
            })
            .OrderByDescending(x => x.OverdueAmount)
            .ThenByDescending(x => x.OpenAmount)
            .ThenBy(x => x.StudentName)
            .ToList();
    }

    private object BuildStudentFinancialStatusesEnvelope(List<StudentFinancialStatusItem> items)
        => new
        {
            delinquentStudents = items.Count(x => x.Status == "Delinquent"),
            dueSoonStudents = items.Count(x => x.Status == "DueSoon"),
            items
        };

    private IActionResult? ValidateReceivableRequest(UpsertReceivableRequest request)
    {
        if (request.StudentId == Guid.Empty)
        {
            return BadRequest("Selecione o aluno da cobrança.");
        }

        if (string.IsNullOrWhiteSpace(request.StudentNameSnapshot))
        {
            return BadRequest("O nome do aluno é obrigatório para registrar a cobrança.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest("A descrição da cobrança é obrigatória.");
        }

        if (request.Amount <= 0)
        {
            return BadRequest("O valor da cobrança deve ser maior que zero.");
        }

        return null;
    }

    private async Task<FinancialCategory?> ResolveCategoryAsync(Guid schoolId, Guid? categoryId)
    {
        if (!categoryId.HasValue)
        {
            return null;
        }

        return await _dbContext.FinancialCategories.FirstOrDefaultAsync(x =>
            x.Id == categoryId.Value &&
            x.SchoolId == schoolId &&
            x.IsActive);
    }

    private async Task<CostCenter?> ResolveCostCenterAsync(Guid schoolId, Guid? costCenterId)
    {
        if (!costCenterId.HasValue)
        {
            return null;
        }

        return await _dbContext.CostCenters.FirstOrDefaultAsync(x =>
            x.Id == costCenterId.Value &&
            x.SchoolId == schoolId &&
            x.IsActive);
    }

    private static void ApplyReceivableStatus(AccountsReceivableEntry entry, bool cancelled)
    {
        if (cancelled)
        {
            entry.Status = ReceivableStatus.Cancelled;
            return;
        }

        if (entry.PaidAmount <= 0)
        {
            entry.Status = ReceivableStatus.Open;
            return;
        }

        if (entry.PaidAmount >= entry.Amount)
        {
            entry.Status = ReceivableStatus.Paid;
            return;
        }

        entry.Status = ReceivableStatus.PartiallyPaid;
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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

    public sealed record UpsertReceivableRequest(
        Guid StudentId,
        string StudentNameSnapshot,
        Guid? EnrollmentId,
        Guid? CategoryId,
        string? CategoryName,
        Guid? CostCenterId,
        decimal Amount,
        DateTime DueAtUtc,
        string Description,
        string? Notes);

    public sealed record RegisterReceivablePaymentRequest(
        decimal Amount,
        DateTime PaidAtUtc,
        string? Note);

    private sealed record StudentFinancialStatusItem(
        Guid StudentId,
        string StudentName,
        string Status,
        decimal OpenAmount,
        decimal OverdueAmount,
        int OpenReceivables,
        int OverdueReceivables,
        DateTime? NextDueAtUtc);
}
