using KiteFlow.BuildingBlocks.MultiTenancy;

namespace KiteFlow.Services.Equipment.Api.Domain;

public sealed class EquipmentUsageLog : TenantScopedEntity
{
    public Guid EquipmentId { get; set; }

    public Guid LessonId { get; set; }

    public Guid CheckoutItemId { get; set; }

    public int UsageMinutes { get; set; }

    public EquipmentCondition ConditionAfter { get; set; }

    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;

    public EquipmentItem? Equipment { get; set; }
}
