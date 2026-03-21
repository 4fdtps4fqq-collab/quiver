using KiteFlow.BuildingBlocks.MultiTenancy;

namespace KiteFlow.Services.Finance.Api.Domain;

public sealed class FinancialReconciliationRecord : TenantScopedEntity
{
    public FinancialEntryKind EntryKind { get; set; }

    public Guid EntryId { get; set; }

    public decimal AmountSnapshot { get; set; }

    public DateTime ReconciledAtUtc { get; set; } = DateTime.UtcNow;

    public string ReconciledByUserId { get; set; } = string.Empty;

    public string? Note { get; set; }
}
