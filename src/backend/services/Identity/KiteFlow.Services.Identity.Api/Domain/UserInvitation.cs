namespace KiteFlow.Services.Identity.Api.Domain;

public sealed class UserInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SchoolId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public PlatformRole Role { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? AcceptedAtUtc { get; set; }

    public Guid? AcceptedUserId { get; set; }

    public DateTime? CancelledAtUtc { get; set; }
}
