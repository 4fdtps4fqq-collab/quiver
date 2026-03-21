using KiteFlow.BuildingBlocks.MultiTenancy;

namespace KiteFlow.Services.Finance.Api.Domain;

public sealed class ExpenseEntry : TenantScopedEntity
{
    public string? SourceType { get; set; }

    public Guid? SourceId { get; set; }

    public ExpenseCategory Category { get; set; }

    public Guid? CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public Guid? CostCenterId { get; set; }

    public string? CostCenterName { get; set; }

    public decimal Amount { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? Vendor { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ReconciledAtUtc { get; set; }

    public string? ReconciledByUserId { get; set; }

    public string? ReconciliationNote { get; set; }
}
