using KiteFlow.Services.Finance.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Finance.Api.Controllers;

[ApiController]
[Authorize(Policy = "SystemAdminOnly")]
[Route("api/v1/system/tenants")]
public sealed class SystemTenantsController : ControllerBase
{
    private readonly FinanceDbContext _dbContext;

    public SystemTenantsController(FinanceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpDelete("{schoolId:guid}")]
    public async Task<IActionResult> DeleteTenant(Guid schoolId)
    {
        var payments = await _dbContext.AccountsReceivablePayments.Where(x => x.SchoolId == schoolId).ToListAsync();
        var receivables = await _dbContext.AccountsReceivableEntries.Where(x => x.SchoolId == schoolId).ToListAsync();
        var payablePayments = await _dbContext.AccountsPayablePayments.Where(x => x.SchoolId == schoolId).ToListAsync();
        var payables = await _dbContext.AccountsPayableEntries.Where(x => x.SchoolId == schoolId).ToListAsync();
        var revenues = await _dbContext.RevenueEntries.Where(x => x.SchoolId == schoolId).ToListAsync();
        var expenses = await _dbContext.ExpenseEntries.Where(x => x.SchoolId == schoolId).ToListAsync();
        var categories = await _dbContext.FinancialCategories.Where(x => x.SchoolId == schoolId).ToListAsync();
        var costCenters = await _dbContext.CostCenters.Where(x => x.SchoolId == schoolId).ToListAsync();
        var reconciliations = await _dbContext.FinancialReconciliationRecords.Where(x => x.SchoolId == schoolId).ToListAsync();

        if (payments.Count > 0) _dbContext.AccountsReceivablePayments.RemoveRange(payments);
        if (receivables.Count > 0) _dbContext.AccountsReceivableEntries.RemoveRange(receivables);
        if (payablePayments.Count > 0) _dbContext.AccountsPayablePayments.RemoveRange(payablePayments);
        if (payables.Count > 0) _dbContext.AccountsPayableEntries.RemoveRange(payables);
        if (revenues.Count > 0) _dbContext.RevenueEntries.RemoveRange(revenues);
        if (expenses.Count > 0) _dbContext.ExpenseEntries.RemoveRange(expenses);
        if (reconciliations.Count > 0) _dbContext.FinancialReconciliationRecords.RemoveRange(reconciliations);
        if (categories.Count > 0) _dbContext.FinancialCategories.RemoveRange(categories);
        if (costCenters.Count > 0) _dbContext.CostCenters.RemoveRange(costCenters);

        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            deletedAtUtc = DateTime.UtcNow,
            schoolId
        });
    }
}
