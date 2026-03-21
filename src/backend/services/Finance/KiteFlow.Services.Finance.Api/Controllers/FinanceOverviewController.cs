using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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
public sealed class FinanceOverviewController : ControllerBase
{
    private readonly FinanceDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public FinanceOverviewController(
        FinanceDbContext dbContext,
        ICurrentTenant currentTenant,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? costCenterId)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            return BadRequest("A data inicial deve ser menor ou igual à data final.");
        }

        var revenueQuery = _dbContext.RevenueEntries.Where(x => x.SchoolId == schoolId);
        var expenseQuery = _dbContext.ExpenseEntries.Where(x => x.SchoolId == schoolId);
        var receivableQuery = _dbContext.AccountsReceivableEntries.Where(x => x.SchoolId == schoolId && x.Status != ReceivableStatus.Cancelled);
        var payableQuery = _dbContext.AccountsPayableEntries.Where(x => x.SchoolId == schoolId && x.Status != PayableStatus.Cancelled);

        if (fromUtc.HasValue)
        {
            revenueQuery = revenueQuery.Where(x => x.RecognizedAtUtc >= fromUtc.Value);
            expenseQuery = expenseQuery.Where(x => x.OccurredAtUtc >= fromUtc.Value);
            receivableQuery = receivableQuery.Where(x => x.DueAtUtc >= fromUtc.Value);
            payableQuery = payableQuery.Where(x => x.DueAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            revenueQuery = revenueQuery.Where(x => x.RecognizedAtUtc <= toUtc.Value);
            expenseQuery = expenseQuery.Where(x => x.OccurredAtUtc <= toUtc.Value);
            receivableQuery = receivableQuery.Where(x => x.DueAtUtc <= toUtc.Value);
            payableQuery = payableQuery.Where(x => x.DueAtUtc <= toUtc.Value);
        }

        if (categoryId.HasValue)
        {
            revenueQuery = revenueQuery.Where(x => x.CategoryId == categoryId.Value);
            expenseQuery = expenseQuery.Where(x => x.CategoryId == categoryId.Value);
            receivableQuery = receivableQuery.Where(x => x.CategoryId == categoryId.Value);
            payableQuery = payableQuery.Where(x => x.CategoryId == categoryId.Value);
        }

        if (costCenterId.HasValue)
        {
            revenueQuery = revenueQuery.Where(x => x.CostCenterId == costCenterId.Value);
            expenseQuery = expenseQuery.Where(x => x.CostCenterId == costCenterId.Value);
            receivableQuery = receivableQuery.Where(x => x.CostCenterId == costCenterId.Value);
            payableQuery = payableQuery.Where(x => x.CostCenterId == costCenterId.Value);
        }

        var academicsFinancials = await GetAcademicsFinancialsAsync(schoolId, fromUtc, toUtc, HttpContext.RequestAborted);

        var totalRevenue = await revenueQuery.SumAsync(x => (decimal?)x.Amount) ?? 0m;
        var manualExpenseTotal = await expenseQuery.SumAsync(x => (decimal?)x.Amount) ?? 0m;
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

        var revenueByCategory = await revenueQuery
            .GroupBy(x => x.Category)
            .Select(group => new
            {
                category = group.Key,
                totalAmount = group.Sum(x => x.Amount),
                entries = group.Count()
            })
            .OrderByDescending(x => x.totalAmount)
            .ToListAsync();

        var expenseByCategory = await expenseQuery
            .GroupBy(x => x.CategoryName ?? x.Category.ToString())
            .Select(group => new
            {
                category = group.Key,
                totalAmount = group.Sum(x => x.Amount),
                entries = group.Count()
            })
            .OrderByDescending(x => x.totalAmount)
            .ToListAsync();

        if (academicsFinancials.InstructorPayrollExpense > 0)
        {
            var payrollCategory = expenseByCategory.FirstOrDefault(x => x.category == "Folha" || x.category == "Payroll");
            if (payrollCategory is null)
            {
                expenseByCategory.Add(new
                {
                    category = "Folha",
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

        expenseByCategory = expenseByCategory.OrderByDescending(x => x.totalAmount).ToList();

        var receivables = await receivableQuery
            .Select(x => new
            {
                x.StudentId,
                x.Amount,
                x.PaidAmount,
                x.DueAtUtc,
                x.Status
            })
            .ToListAsync();

        var payables = await payableQuery
            .Select(x => new
            {
                x.Amount,
                x.PaidAmount,
                x.DueAtUtc,
                x.Status
            })
            .ToListAsync();

        var now = DateTime.UtcNow;
        var openReceivables = receivables.Where(x => x.Status != ReceivableStatus.Paid && x.Amount > x.PaidAmount).ToList();
        var overdueReceivables = openReceivables.Where(x => x.DueAtUtc < now).ToList();
        var openPayables = payables.Where(x => x.Status != PayableStatus.Paid && x.Amount > x.PaidAmount).ToList();
        var overduePayables = openPayables.Where(x => x.DueAtUtc < now).ToList();

        var delinquentStudents = overdueReceivables.Select(x => x.StudentId).Distinct().Count();
        var dueSoonStudents = openReceivables
            .Where(x => x.DueAtUtc >= now)
            .Select(x => x.StudentId)
            .Distinct()
            .Count();

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

        var costCenterRevenue = await revenueQuery
            .GroupBy(x => x.CostCenterName ?? "Sem centro de custo")
            .Select(group => new
            {
                name = group.Key,
                revenue = group.Sum(x => x.Amount)
            })
            .ToListAsync();

        var costCenterExpense = await expenseQuery
            .GroupBy(x => x.CostCenterName ?? "Sem centro de custo")
            .Select(group => new
            {
                name = group.Key,
                expense = group.Sum(x => x.Amount)
            })
            .ToListAsync();

        var costCenterNames = costCenterRevenue.Select(x => x.name)
            .Union(costCenterExpense.Select(x => x.name))
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        var costCenterMargins = costCenterNames
            .Select(name => new
            {
                costCenterName = name,
                revenue = costCenterRevenue.FirstOrDefault(x => x.name == name)?.revenue ?? 0m,
                expense = costCenterExpense.FirstOrDefault(x => x.name == name)?.expense ?? 0m,
            })
            .Select(x => new
            {
                x.costCenterName,
                x.revenue,
                x.expense,
                grossMargin = x.revenue - x.expense
            })
            .OrderByDescending(x => x.revenue)
            .ToList();

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

        var reconciledRevenueAmount = await revenueQuery
            .Where(x => x.ReconciledAtUtc != null)
            .SumAsync(x => (decimal?)x.Amount) ?? 0m;
        var reconciledExpenseAmount = await expenseQuery
            .Where(x => x.ReconciledAtUtc != null)
            .SumAsync(x => (decimal?)x.Amount) ?? 0m;
        var reconciledReceivableAmount = await receivableQuery
            .Where(x => x.ReconciledAtUtc != null)
            .SumAsync(x => (decimal?)(x.Amount - x.PaidAmount)) ?? 0m;
        var reconciledPayableAmount = await payableQuery
            .Where(x => x.ReconciledAtUtc != null)
            .SumAsync(x => (decimal?)(x.Amount - x.PaidAmount)) ?? 0m;

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
            payablesOpenAmount = openPayables.Sum(x => x.Amount - x.PaidAmount),
            payablesOverdueAmount = overduePayables.Sum(x => x.Amount - x.PaidAmount),
            payablesOpenEntries = openPayables.Count,
            delinquentStudents,
            dueSoonStudents,
            revenueEntries = await revenueQuery.CountAsync(),
            expenseEntries = await expenseQuery.CountAsync(),
            reconciledRevenueAmount,
            unreconciledRevenueAmount = totalRevenue - reconciledRevenueAmount,
            reconciledExpenseAmount,
            unreconciledExpenseAmount = manualExpenseTotal - reconciledExpenseAmount,
            reconciledReceivableAmount,
            unreconciledReceivableAmount = openReceivables.Sum(x => x.Amount - x.PaidAmount) - reconciledReceivableAmount,
            reconciledPayableAmount,
            unreconciledPayableAmount = openPayables.Sum(x => x.Amount - x.PaidAmount) - reconciledPayableAmount,
            revenueBySource,
            revenueByCategory,
            expenseByCategory,
            costCenterMargins,
            cashflowSeries,
            marginByCourse = academicsFinancials.ByCourse,
            marginByInstructor = academicsFinancials.ByInstructor
        });
    }

    private async Task<AcademicsFinancialSnapshot> GetAcademicsFinancialsAsync(
        Guid schoolId,
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

        var sharedKey = _configuration["InternalServiceAuth:SharedKey"];
        if (!string.IsNullOrWhiteSpace(sharedKey))
        {
            client.DefaultRequestHeaders.Remove("X-KiteFlow-Internal-Key");
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-KiteFlow-Internal-Key", sharedKey);
        }

        var overviewResponse = await client.GetAsync($"/api/v1/academics/overview{BuildQueryString(fromUtc, toUtc)}", cancellationToken);
        if (!overviewResponse.IsSuccessStatusCode)
        {
            return AcademicsFinancialSnapshot.Empty;
        }

        var analyticsResponse = await client.GetAsync(
            $"/api/v1/internal/academics/financial-analytics{BuildInternalQueryString(schoolId, fromUtc, toUtc)}",
            cancellationToken);

        await using var overviewStream = await overviewResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var overviewDocument = await JsonDocument.ParseAsync(overviewStream, cancellationToken: cancellationToken);
        var overviewRoot = overviewDocument.RootElement;

        var payrollSeries = new List<PayrollSeriesItem>();
        if (overviewRoot.TryGetProperty("instructorPayrollSeries", out var payrollSeriesElement) &&
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

        var courseMargins = new List<MarginSummaryItem>();
        var instructorMargins = new List<MarginSummaryItem>();

        if (analyticsResponse.IsSuccessStatusCode)
        {
            await using var analyticsStream = await analyticsResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var analyticsDocument = await JsonDocument.ParseAsync(analyticsStream, cancellationToken: cancellationToken);
            var analyticsRoot = analyticsDocument.RootElement;

            courseMargins = ReadMarginItems(analyticsRoot, "byCourse", "courseId", "courseName");
            instructorMargins = ReadMarginItems(analyticsRoot, "byInstructor", "instructorId", "instructorName");
        }

        return new AcademicsFinancialSnapshot(
            overviewRoot.TryGetProperty("instructorPayrollExpense", out var instructorPayrollExpenseElement) &&
            instructorPayrollExpenseElement.TryGetDecimal(out var instructorPayrollExpense)
                ? instructorPayrollExpense
                : 0m,
            overviewRoot.TryGetProperty("realizedInstructionMinutes", out var realizedInstructionMinutesElement) &&
            realizedInstructionMinutesElement.TryGetInt32(out var realizedInstructionMinutes)
                ? realizedInstructionMinutes
                : 0,
            payrollSeries,
            courseMargins,
            instructorMargins);
    }

    private static List<MarginSummaryItem> ReadMarginItems(
        JsonElement root,
        string arrayProperty,
        string idProperty,
        string nameProperty)
    {
        var items = new List<MarginSummaryItem>();
        if (!root.TryGetProperty(arrayProperty, out var arrayElement) || arrayElement.ValueKind != JsonValueKind.Array)
        {
            return items;
        }

        foreach (var item in arrayElement.EnumerateArray())
        {
            Guid? id = null;
            if (item.TryGetProperty(idProperty, out var idElement) &&
                idElement.ValueKind == JsonValueKind.String &&
                Guid.TryParse(idElement.GetString(), out var parsedId))
            {
                id = parsedId;
            }

            var name = item.TryGetProperty(nameProperty, out var nameElement) ? nameElement.GetString() ?? "-" : "-";
            var recognizedRevenue = item.TryGetProperty("recognizedRevenue", out var recognizedRevenueElement) &&
                                    recognizedRevenueElement.TryGetDecimal(out var recognizedRevenueValue)
                ? recognizedRevenueValue
                : 0m;
            var deliveredRevenue = item.TryGetProperty("deliveredRevenue", out var deliveredRevenueElement) &&
                                   deliveredRevenueElement.TryGetDecimal(out var deliveredRevenueValue)
                ? deliveredRevenueValue
                : 0m;
            var payrollExpense = item.TryGetProperty("payrollExpense", out var payrollExpenseElement) &&
                                 payrollExpenseElement.TryGetDecimal(out var payrollExpenseValue)
                ? payrollExpenseValue
                : 0m;
            var grossMargin = item.TryGetProperty("grossMargin", out var grossMarginElement) &&
                              grossMarginElement.TryGetDecimal(out var grossMarginValue)
                ? grossMarginValue
                : 0m;
            var realizedLessons = item.TryGetProperty("realizedLessons", out var lessonsElement) &&
                                  lessonsElement.TryGetInt32(out var lessonsValue)
                ? lessonsValue
                : 0;
            var realizedMinutes = item.TryGetProperty("realizedMinutes", out var minutesElement) &&
                                  minutesElement.TryGetInt32(out var minutesValue)
                ? minutesValue
                : 0;

            items.Add(new MarginSummaryItem(
                id,
                name,
                recognizedRevenue,
                deliveredRevenue,
                payrollExpense,
                grossMargin,
                realizedLessons,
                realizedMinutes));
        }

        return items;
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

    private static string BuildInternalQueryString(Guid schoolId, DateTime? fromUtc, DateTime? toUtc)
    {
        var builder = new StringBuilder($"?schoolId={Uri.EscapeDataString(schoolId.ToString())}");
        if (fromUtc.HasValue)
        {
            builder.Append("&fromUtc=");
            builder.Append(Uri.EscapeDataString(fromUtc.Value.ToString("O")));
        }

        if (toUtc.HasValue)
        {
            builder.Append("&toUtc=");
            builder.Append(Uri.EscapeDataString(toUtc.Value.ToString("O")));
        }

        return builder.ToString();
    }

    private sealed record AcademicsFinancialSnapshot(
        decimal InstructorPayrollExpense,
        int RealizedInstructionMinutes,
        IReadOnlyList<PayrollSeriesItem> PayrollSeries,
        IReadOnlyList<MarginSummaryItem> ByCourse,
        IReadOnlyList<MarginSummaryItem> ByInstructor)
    {
        public static AcademicsFinancialSnapshot Empty { get; } = new(
            0m,
            0,
            Array.Empty<PayrollSeriesItem>(),
            Array.Empty<MarginSummaryItem>(),
            Array.Empty<MarginSummaryItem>());
    }

    private sealed record PayrollSeriesItem(DateTime Day, decimal Amount);

    private sealed record MarginSummaryItem(
        Guid? Id,
        string Name,
        decimal RecognizedRevenue,
        decimal DeliveredRevenue,
        decimal PayrollExpense,
        decimal GrossMargin,
        int RealizedLessons,
        int RealizedMinutes);
}
