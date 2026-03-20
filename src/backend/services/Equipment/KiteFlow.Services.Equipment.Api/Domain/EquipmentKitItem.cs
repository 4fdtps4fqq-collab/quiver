using KiteFlow.BuildingBlocks.MultiTenancy;

namespace KiteFlow.Services.Equipment.Api.Domain;

public sealed class EquipmentKitItem : TenantScopedEntity
{
    public Guid KitId { get; set; }

    public Guid EquipmentId { get; set; }

    public EquipmentKit? Kit { get; set; }

    public EquipmentItem? Equipment { get; set; }
}
