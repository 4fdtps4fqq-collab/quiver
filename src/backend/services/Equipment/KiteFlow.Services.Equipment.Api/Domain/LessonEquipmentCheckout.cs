using KiteFlow.BuildingBlocks.MultiTenancy;

namespace KiteFlow.Services.Equipment.Api.Domain;

public sealed class LessonEquipmentCheckout : TenantScopedEntity
{
    public Guid LessonId { get; set; }

    public DateTime CheckedOutAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CheckedInAtUtc { get; set; }

    public Guid CreatedByUserId { get; set; }

    public Guid? CheckedInByUserId { get; set; }

    public string? NotesBefore { get; set; }

    public string? NotesAfter { get; set; }
}
