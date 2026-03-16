using KiteFlow.BuildingBlocks.MultiTenancy;

namespace KiteFlow.Services.Academics.Api.Domain;

public sealed class Student : TenantScopedEntity
{
    public Guid? IdentityUserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? PostalCode { get; set; }

    public string? Street { get; set; }

    public string? StreetNumber { get; set; }

    public string? AddressComplement { get; set; }

    public string? Neighborhood { get; set; }

    public DateOnly? BirthDate { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? MedicalNotes { get; set; }

    public string? EmergencyContactName { get; set; }

    public string? EmergencyContactPhone { get; set; }

    public DateTime? FirstStandUpAtUtc { get; set; }

    public bool IsActive { get; set; } = true;
}
