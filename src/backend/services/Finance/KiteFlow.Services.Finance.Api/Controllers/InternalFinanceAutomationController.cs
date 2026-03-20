using KiteFlow.Services.Finance.Api.Data;
using KiteFlow.Services.Finance.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace KiteFlow.Services.Finance.Api.Controllers;

[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/v1/internal/finance")]
public sealed class InternalFinanceAutomationController : ControllerBase
{
    private readonly FinanceDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public InternalFinanceAutomationController(FinanceDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    [HttpPost("revenues/automation")]
    public async Task<IActionResult> UpsertAutomatedRevenue([FromBody] UpsertAutomatedRevenueRequest request)
    {
        if (!IsInternalGatewayCall())
        {
            return Unauthorized("Esta rota interna aceita apenas chamadas autenticadas entre serviços.");
        }

        if (request.SchoolId == Guid.Empty)
        {
            return BadRequest("A escola da receita automática é obrigatória.");
        }

        if (request.SourceId == Guid.Empty)
        {
            return BadRequest("O identificador de origem é obrigatório.");
        }

        var entry = await _dbContext.RevenueEntries.FirstOrDefaultAsync(x =>
            x.SchoolId == request.SchoolId &&
            x.SourceType == request.SourceType &&
            x.SourceId == request.SourceId);

        if (!request.IsActive)
        {
            if (entry is not null)
            {
                _dbContext.RevenueEntries.Remove(entry);
                await _dbContext.SaveChangesAsync();
            }

            return Ok(new { synchronized = true, removed = true });
        }

        if (request.Amount <= 0)
        {
            return BadRequest("O valor da receita automática deve ser maior que zero.");
        }

        if (string.IsNullOrWhiteSpace(request.Category) || string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest("Categoria e descrição são obrigatórias para a receita automática.");
        }

        if (entry is null)
        {
            entry = new RevenueEntry
            {
                SchoolId = request.SchoolId,
                SourceType = request.SourceType,
                SourceId = request.SourceId
            };

            _dbContext.RevenueEntries.Add(entry);
        }

        entry.Category = request.Category.Trim();
        entry.Amount = request.Amount;
        entry.RecognizedAtUtc = request.RecognizedAtUtc;
        entry.Description = request.Description.Trim();

        await _dbContext.SaveChangesAsync();
        return Ok(new { synchronized = true, removed = false, revenueId = entry.Id });
    }

    [HttpPost("expenses/automation")]
    public async Task<IActionResult> UpsertAutomatedExpense([FromBody] UpsertAutomatedExpenseRequest request)
    {
        if (!IsInternalGatewayCall())
        {
            return Unauthorized("Esta rota interna aceita apenas chamadas autenticadas entre serviços.");
        }

        if (request.SchoolId == Guid.Empty)
        {
            return BadRequest("A escola da despesa automática é obrigatória.");
        }

        if (request.SourceId == Guid.Empty)
        {
            return BadRequest("O identificador de origem é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(request.SourceType))
        {
            return BadRequest("O tipo de origem da despesa automática é obrigatório.");
        }

        var normalizedSourceType = request.SourceType.Trim();

        var entry = await _dbContext.ExpenseEntries.FirstOrDefaultAsync(x =>
            x.SchoolId == request.SchoolId &&
            x.SourceType == normalizedSourceType &&
            x.SourceId == request.SourceId);

        if (!request.IsActive)
        {
            if (entry is not null)
            {
                _dbContext.ExpenseEntries.Remove(entry);
                await _dbContext.SaveChangesAsync();
            }

            return Ok(new { synchronized = true, removed = true });
        }

        if (request.Amount <= 0)
        {
            return BadRequest("O valor da despesa automática deve ser maior que zero.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest("A descrição é obrigatória para a despesa automática.");
        }

        if (entry is null)
        {
            entry = new ExpenseEntry
            {
                SchoolId = request.SchoolId,
                SourceType = normalizedSourceType,
                SourceId = request.SourceId
            };

            _dbContext.ExpenseEntries.Add(entry);
        }

        entry.SourceType = normalizedSourceType;
        entry.SourceId = request.SourceId;
        entry.Category = request.Category;
        entry.Amount = request.Amount;
        entry.Description = request.Description.Trim();
        entry.Vendor = string.IsNullOrWhiteSpace(request.Vendor) ? null : request.Vendor.Trim();
        entry.OccurredAtUtc = request.OccurredAtUtc;

        await _dbContext.SaveChangesAsync();
        return Ok(new { synchronized = true, removed = false, expenseId = entry.Id });
    }

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

    public sealed record UpsertAutomatedRevenueRequest(
        Guid SchoolId,
        RevenueSourceType SourceType,
        Guid SourceId,
        string Category,
        decimal Amount,
        DateTime RecognizedAtUtc,
        string Description,
        bool IsActive);

    public sealed record UpsertAutomatedExpenseRequest(
        Guid SchoolId,
        string SourceType,
        Guid SourceId,
        ExpenseCategory Category,
        decimal Amount,
        DateTime OccurredAtUtc,
        string Description,
        string? Vendor,
        bool IsActive);
}
