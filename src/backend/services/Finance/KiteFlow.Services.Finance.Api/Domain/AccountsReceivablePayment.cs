using KiteFlow.BuildingBlocks.MultiTenancy;

namespace KiteFlow.Services.Finance.Api.Domain;

public sealed class AccountsReceivablePayment : TenantScopedEntity
{
    public Guid ReceivableId { get; set; }

    public decimal Amount { get; set; }

    public DateTime PaidAtUtc { get; set; } = DateTime.UtcNow;

    public string? Note { get; set; }
}
