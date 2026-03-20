using KiteFlow.BuildingBlocks.MultiTenancy;

namespace KiteFlow.Services.Equipment.Api.Domain;

public sealed class EquipmentReservation : TenantScopedEntity
{
    public Guid LessonId { get; set; }

    public DateTime ReservedFromUtc { get; set; }

    public DateTime ReservedUntilUtc { get; set; }

    public string? Notes { get; set; }

    public Guid CreatedByUserId { get; set; }
}
