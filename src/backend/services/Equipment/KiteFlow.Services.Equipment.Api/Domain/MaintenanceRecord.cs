using KiteFlow.BuildingBlocks.MultiTenancy;

namespace KiteFlow.Services.Equipment.Api.Domain;

public sealed class MaintenanceRecord : TenantScopedEntity
{
    public Guid EquipmentId { get; set; }

    public DateTime ServiceDateUtc { get; set; } = DateTime.UtcNow;

    public int UsageMinutesAtService { get; set; }

    public decimal? Cost { get; set; }

    public MaintenanceServiceCategory ServiceCategory { get; set; } = MaintenanceServiceCategory.Preventive;

    public MaintenanceFinancialEffect FinancialEffect { get; set; } = MaintenanceFinancialEffect.None;

    public string Description { get; set; } = string.Empty;

    public string? PerformedBy { get; set; }

    public string? CounterpartyName { get; set; }

    public EquipmentItem? Equipment { get; set; }
}
