using KiteFlow.BuildingBlocks.MultiTenancy;

namespace KiteFlow.Services.Academics.Api.Domain;

public sealed class Instructor : TenantScopedEntity
{
    public Guid? IdentityUserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Specialties { get; set; }

    public string? AvailabilityJson { get; set; }

    public decimal HourlyRate { get; set; }

    public bool IsActive { get; set; } = true;
}
