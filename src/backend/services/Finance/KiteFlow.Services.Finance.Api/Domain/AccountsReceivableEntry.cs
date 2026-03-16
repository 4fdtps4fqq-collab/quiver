using KiteFlow.BuildingBlocks.MultiTenancy;

namespace KiteFlow.Services.Finance.Api.Domain;

public sealed class AccountsReceivableEntry : TenantScopedEntity
{
    public Guid StudentId { get; set; }

    public Guid? EnrollmentId { get; set; }

    public string StudentNameSnapshot { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public decimal Amount { get; set; }

    public decimal PaidAmount { get; set; }

    public DateTime DueAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastPaymentAtUtc { get; set; }

    public ReceivableStatus Status { get; set; } = ReceivableStatus.Open;
}
