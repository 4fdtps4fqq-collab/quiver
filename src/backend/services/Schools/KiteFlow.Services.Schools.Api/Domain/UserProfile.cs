using KiteFlow.BuildingBlocks.MultiTenancy;

namespace KiteFlow.Services.Schools.Api.Domain;

public sealed class UserProfile : TenantScopedEntity
{
    public Guid IdentityUserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? Cpf { get; set; }

    public string? Phone { get; set; }

    public string? PostalCode { get; set; }

    public string? Street { get; set; }

    public string? StreetNumber { get; set; }

    public string? AddressComplement { get; set; }

    public string? Neighborhood { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? AvatarUrl { get; set; }

    public bool IsActive { get; set; } = true;
}
