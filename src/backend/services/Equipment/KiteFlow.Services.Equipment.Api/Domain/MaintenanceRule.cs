using KiteFlow.BuildingBlocks.MultiTenancy;

namespace KiteFlow.Services.Equipment.Api.Domain;

public sealed class MaintenanceRule : TenantScopedEntity
{
    public EquipmentType EquipmentType { get; set; }

    public string PlanName { get; set; } = string.Empty;

    public MaintenanceServiceCategory ServiceCategory { get; set; } = MaintenanceServiceCategory.Preventive;

    public int? ServiceEveryMinutes { get; set; }

    public int? ServiceEveryDays { get; set; }

    public int? WarningLeadMinutes { get; set; }

    public int? CriticalLeadMinutes { get; set; }

    public int? WarningLeadDays { get; set; }

    public int? CriticalLeadDays { get; set; }

    public string? Checklist { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
}
