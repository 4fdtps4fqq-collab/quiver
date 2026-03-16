namespace KiteFlow.Services.Identity.Api.Domain;

public sealed class RefreshSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserAccountId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public string? DeviceName { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public UserAccount? UserAccount { get; set; }
}
