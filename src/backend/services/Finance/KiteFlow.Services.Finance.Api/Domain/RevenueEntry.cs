using KiteFlow.BuildingBlocks.MultiTenancy;

namespace KiteFlow.Services.Finance.Api.Domain;

public sealed class RevenueEntry : TenantScopedEntity
{
    public RevenueSourceType SourceType { get; set; }

    public Guid SourceId { get; set; }

    public string Category { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateTime RecognizedAtUtc { get; set; } = DateTime.UtcNow;

    public string Description { get; set; } = string.Empty;
}
