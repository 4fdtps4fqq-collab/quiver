using KiteFlow.BuildingBlocks.MultiTenancy;
using KiteFlow.Services.Finance.Api.Data;
using KiteFlow.Services.Finance.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace KiteFlow.Services.Finance.Api.Controllers;

[ApiController]
[Authorize(Policy = "FinanceAccess")]
[Route("api/v1/finance")]
public sealed class FinanceOverviewController : ControllerBase
{
    private readonly FinanceDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;
    private readonly IHttpClientFactory _httpClientFactory;

    public FinanceOverviewController(
        FinanceDbContext dbContext,
        ICurrentTenant currentTenant,
        IHttpClientFactory httpClientFactory)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            return BadRequest("A data inicial deve ser menor ou igual a data final.");
        }

        var revenueQuery = _dbContext.RevenueEntries.Where(x => x.SchoolId == schoolId);
        if (fromUtc.HasValue)
        {
            revenueQuery = revenueQuery.Where(x => x.RecognizedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            revenueQuery = revenueQuery.Where(x => x.RecognizedAtUtc <= toUtc.Value);
        }

        var expenseQuery = _dbContext.ExpenseEntries.Where(x => x.SchoolId == schoolId);
        if (fromUtc.HasValue)
        {
            expenseQuery = expenseQuery.Where(x => x.OccurredAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            expenseQuery = expenseQuery.Where(x => x.OccurredAtUtc <= toUtc.Value);
        }

        var academicsFinancials = await GetAcademicsFinancialsAsync(fromUtc, toUtc, HttpContext.RequestAborted);

        var totalRevenue = await revenueQuery
            .SumAsync(x => (decimal?)x.Amount) ?? 0m;

        var manualExpenseTotal = await expenseQuery
            .SumAsync(x => (decimal?)x.Amount) ?? 0m;

        var totalExpense = manualExpenseTotal + academicsFinancials.InstructorPayrollExpense;

        var revenueBySource = await revenueQuery
            .GroupBy(x => x.SourceType)
            .Select(group => new
            {
                sourceType = group.Key.ToString(),
                totalAmount = group.Sum(x => x.Amount),
                entries = group.Count()
            })
            .OrderByDescending(x => x.totalAmount)
            .ToListAsync();

        var expenseByCategory = await expenseQuery
            .GroupBy(x => x.Category)
            .Select(group => new
            {
                category = group.Key.ToString(),
                totalAmount = group.Sum(x => x.Amount),
                entries = group.Count()
            })
            .OrderByDescending(x => x.totalAmount)
            .ToListAsync();

        if (academicsFinancials.InstructorPayrollExpense > 0)
        {
            var payrollCategory = expenseByCategory.FirstOrDefault(x => x.category == "Payroll");
            if (payrollCategory is null)
            {
                expenseByCategory.Add(new
                {
                    category = "Payroll",
                    totalAmount = academicsFinancials.InstructorPayrollExpense,
                    entries = 1
                });
            }
            else
            {
                expenseByCategory.Remove(payrollCategory);
                expenseByCategory.Add(new
                {
                    category = payrollCategory.category,
                    totalAmount = payrollCategory.totalAmount + academicsFinancials.InstructorPayrollExpense,
                    entries = payrollCategory.entries + 1
                });
            }
        }

        expenseByCategory = expenseByCategory
            .OrderByDescending(x => x.totalAmount)
            .ToList();

        var revenueSeries = await revenueQuery
            .GroupBy(x => x.RecognizedAtUtc.Date)
            .Select(group => new
            {
                day = group.Key,
                revenue = group.Sum(x => x.Amount)
            })
            .ToListAsync();

        var expenseSeries = await expenseQuery
            .GroupBy(x => x.OccurredAtUtc.Date)
            .Select(group => new
            {
                day = group.Key,
                expense = group.Sum(x => x.Amount)
            })
            .ToListAsync();

        var receivables = await _dbContext.AccountsReceivableEntries
            .Where(x => x.SchoolId == schoolId && x.Status != ReceivableStatus.Cancelled)
            .Select(x => new
            {
                x.StudentId,
                x.Amount,
                x.PaidAmount,
                x.DueAtUtc,
                x.Status
            })
            .ToListAsync();

        var openReceivables = receivables
            .Where(x => x.Status != ReceivableStatus.Paid && x.Amount > x.PaidAmount)
            .ToList();
        var overdueReceivables = openReceivables.Where(x => x.DueAtUtc < DateTime.UtcNow).ToList();
        var delinquentStudents = overdueReceivables.Select(x => x.StudentId).Distinct().Count();
        var dueSoonStudents = openReceivables
            .Where(x => x.DueAtUtc >= DateTime.UtcNow)
            .Select(x => x.StudentId)
            .Distinct()
            .Count();

        var cashflowSeries = revenueSeries
            .Select(x => x.day)
            .Union(expenseSeries.Select(x => x.day))
            .Union(academicsFinancials.PayrollSeries.Select(x => x.Day))
            .Distinct()
            .OrderBy(x => x)
            .Select(day => new
            {
                bucketStartUtc = day,
                bucketLabel = day.ToString("dd/MM"),
                revenue = revenueSeries.FirstOrDefault(x => x.day == day)?.revenue ?? 0m,
                expense = (expenseSeries.FirstOrDefault(x => x.day == day)?.expense ?? 0m) +
                          (academicsFinancials.PayrollSeries.FirstOrDefault(x => x.Day == day)?.Amount ?? 0m),
                net = (revenueSeries.FirstOrDefault(x => x.day == day)?.revenue ?? 0m) -
                      ((expenseSeries.FirstOrDefault(x => x.day == day)?.expense ?? 0m) +
                       (academicsFinancials.PayrollSeries.FirstOrDefault(x => x.Day == day)?.Amount ?? 0m))
            })
            .ToList();

        return Ok(new
        {
            fromUtc,
            toUtc,
            totalRevenue,
            manualExpenseTotal,
            instructorPayrollExpense = academicsFinancials.InstructorPayrollExpense,
            realizedInstructionMinutes = academicsFinancials.RealizedInstructionMinutes,
            totalExpense,
            grossMargin = totalRevenue - totalExpense,
            receivablesOpenAmount = openReceivables.Sum(x => x.Amount - x.PaidAmount),
            receivablesOverdueAmount = overdueReceivables.Sum(x => x.Amount - x.PaidAmount),
            receivablesOpenEntries = openReceivables.Count,
            delinquentStudents,
            dueSoonStudents,
            revenueEntries = await revenueQuery.CountAsync(),
            expenseEntries = await expenseQuery.CountAsync(),
            revenueBySource,
            expenseByCategory,
            cashflowSeries
        });
    }

    private async Task<AcademicsFinancialSnapshot> GetAcademicsFinancialsAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("academics");
        var authorization = Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization) && AuthenticationHeaderValue.TryParse(authorization, out var header))
        {
            client.DefaultRequestHeaders.Authorization = header;
        }

        var response = await client.GetAsync($"/api/v1/academics/overview{BuildQueryString(fromUtc, toUtc)}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return AcademicsFinancialSnapshot.Empty;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        var payrollSeries = new List<PayrollSeriesItem>();
        if (root.TryGetProperty("instructorPayrollSeries", out var payrollSeriesElement) &&
            payrollSeriesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in payrollSeriesElement.EnumerateArray())
            {
                if (!item.TryGetProperty("bucketStartUtc", out var bucketStartUtcElement) ||
                    !item.TryGetProperty("instructorPayrollExpense", out var amountElement) ||
                    !bucketStartUtcElement.TryGetDateTime(out var day) ||
                    !amountElement.TryGetDecimal(out var amount))
                {
                    continue;
                }

                payrollSeries.Add(new PayrollSeriesItem(day.Date, amount));
            }
        }

        return new AcademicsFinancialSnapshot(
            root.TryGetProperty("instructorPayrollExpense", out var instructorPayrollExpenseElement) &&
            instructorPayrollExpenseElement.TryGetDecimal(out var instructorPayrollExpense)
                ? instructorPayrollExpense
                : 0m,
            root.TryGetProperty("realizedInstructionMinutes", out var realizedInstructionMinutesElement) &&
            realizedInstructionMinutesElement.TryGetInt32(out var realizedInstructionMinutes)
                ? realizedInstructionMinutes
                : 0,
            payrollSeries);
    }

    private static string BuildQueryString(DateTime? fromUtc, DateTime? toUtc)
    {
        var builder = new StringBuilder();

        if (fromUtc.HasValue)
        {
            builder.Append(builder.Length == 0 ? '?' : '&');
            builder.Append("fromUtc=");
            builder.Append(Uri.EscapeDataString(fromUtc.Value.ToString("O")));
        }

        if (toUtc.HasValue)
        {
            builder.Append(builder.Length == 0 ? '?' : '&');
            builder.Append("toUtc=");
            builder.Append(Uri.EscapeDataString(toUtc.Value.ToString("O")));
        }

        return builder.ToString();
    }

    private sealed record AcademicsFinancialSnapshot(
        decimal InstructorPayrollExpense,
        int RealizedInstructionMinutes,
        IReadOnlyList<PayrollSeriesItem> PayrollSeries)
    {
        public static AcademicsFinancialSnapshot Empty { get; } = new(0m, 0, Array.Empty<PayrollSeriesItem>());
    }

    private sealed record PayrollSeriesItem(DateTime Day, decimal Amount);
}
