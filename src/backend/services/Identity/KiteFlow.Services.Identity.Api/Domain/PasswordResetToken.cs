namespace KiteFlow.Services.Identity.Api.Domain;

public sealed class PasswordResetToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserAccountId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UsedAtUtc { get; set; }

    public string? RequestedIpAddress { get; set; }

    public string? RequestedUserAgent { get; set; }

    public UserAccount? UserAccount { get; set; }
}
