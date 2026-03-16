using KiteFlow.BuildingBlocks.MultiTenancy;

namespace KiteFlow.Services.Equipment.Api.Domain;

public sealed class EquipmentItem : TenantScopedEntity
{
    public Guid StorageId { get; set; }

    public string Name { get; set; } = string.Empty;

    public EquipmentType Type { get; set; }

    public string? TagCode { get; set; }

    public string? Brand { get; set; }

    public string? Model { get; set; }

    public string? SizeLabel { get; set; }

    public EquipmentCondition CurrentCondition { get; set; } = EquipmentCondition.Good;

    public int TotalUsageMinutes { get; set; }

    public DateTime? LastServiceDateUtc { get; set; }

    public int? LastServiceUsageMinutes { get; set; }

    public bool IsActive { get; set; } = true;

    public GearStorage? Storage { get; set; }
}
