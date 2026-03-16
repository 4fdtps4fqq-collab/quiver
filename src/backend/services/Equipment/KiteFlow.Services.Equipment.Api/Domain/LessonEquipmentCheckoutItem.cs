using KiteFlow.BuildingBlocks.MultiTenancy;

namespace KiteFlow.Services.Equipment.Api.Domain;

public sealed class LessonEquipmentCheckoutItem : TenantScopedEntity
{
    public Guid CheckoutId { get; set; }

    public Guid EquipmentId { get; set; }

    public EquipmentCondition ConditionBefore { get; set; }

    public EquipmentCondition? ConditionAfter { get; set; }

    public string? NotesBefore { get; set; }

    public string? NotesAfter { get; set; }

    public LessonEquipmentCheckout? Checkout { get; set; }

    public EquipmentItem? Equipment { get; set; }
}
