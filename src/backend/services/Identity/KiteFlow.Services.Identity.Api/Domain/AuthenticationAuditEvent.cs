namespace KiteFlow.Services.Identity.Api.Domain;

public sealed class AuthenticationAuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? SchoolId { get; set; }

    public Guid? UserAccountId { get; set; }

    public Guid? TargetUserAccountId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Outcome { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? MetadataJson { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
