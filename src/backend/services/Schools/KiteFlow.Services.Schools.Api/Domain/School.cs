namespace KiteFlow.Services.Schools.Api.Domain;

public sealed class School
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string LegalName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Cnpj { get; set; }

    public string? BaseBeachName { get; set; }

    public double? BaseLatitude { get; set; }

    public double? BaseLongitude { get; set; }

    public string Slug { get; set; } = string.Empty;

    public SchoolStatus Status { get; set; } = SchoolStatus.Active;

    public string Timezone { get; set; } = "America/Sao_Paulo";

    public string CurrencyCode { get; set; } = "BRL";

    public string? LogoDataUrl { get; set; }

    public string? PostalCode { get; set; }

    public string? Street { get; set; }

    public string? StreetNumber { get; set; }

    public string? AddressComplement { get; set; }

    public string? Neighborhood { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
