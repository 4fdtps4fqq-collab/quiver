using KiteFlow.BuildingBlocks.MultiTenancy;

namespace KiteFlow.Services.Finance.Api.Domain;

public sealed class AccountsPayableEntry : TenantScopedEntity
{
    public string Description { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public string? Vendor { get; set; }

    public string? SourceType { get; set; }

    public Guid? SourceId { get; set; }

    public Guid? CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public Guid? CostCenterId { get; set; }

    public string? CostCenterName { get; set; }

    public decimal Amount { get; set; }

    public decimal PaidAmount { get; set; }

    public DateTime DueAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastPaymentAtUtc { get; set; }

    public PayableStatus Status { get; set; } = PayableStatus.Open;

    public DateTime? ReconciledAtUtc { get; set; }

    public string? ReconciledByUserId { get; set; }

    public string? ReconciliationNote { get; set; }
}
