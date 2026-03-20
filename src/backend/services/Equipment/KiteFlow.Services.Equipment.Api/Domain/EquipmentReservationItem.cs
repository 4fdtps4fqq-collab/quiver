using KiteFlow.BuildingBlocks.MultiTenancy;

namespace KiteFlow.Services.Equipment.Api.Domain;

public sealed class EquipmentReservationItem : TenantScopedEntity
{
    public Guid ReservationId { get; set; }

    public Guid EquipmentId { get; set; }

    public EquipmentReservation? Reservation { get; set; }

    public EquipmentItem? Equipment { get; set; }
}
