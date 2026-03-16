using System.Text.Json;
using KiteFlow.BuildingBlocks.Authentication;

namespace KiteFlow.Services.Identity.Api.Domain;

public sealed class UserAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? SchoolId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public PlatformRole Role { get; set; }

    public string? PermissionsJson { get; set; }

    public bool IsActive { get; set; } = true;

    public bool MustChangePassword { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAtUtc { get; set; }

    public IReadOnlyCollection<string> GetEffectivePermissions()
    {
        if (Role is PlatformRole.SystemAdmin or PlatformRole.Owner)
        {
            return PlatformPermissions.All;
        }

        if (string.IsNullOrWhiteSpace(PermissionsJson))
        {
            return PlatformPermissions.GetDefaultPermissions(Role.ToString());
        }

        return PlatformPermissions.Normalize(DeserializePermissions(PermissionsJson));
    }

    public void SetPermissions(IEnumerable<string>? permissions)
    {
        if (Role is PlatformRole.SystemAdmin or PlatformRole.Owner)
        {
            PermissionsJson = null;
            return;
        }

        if (permissions is null)
        {
            PermissionsJson = null;
            return;
        }

        PermissionsJson = JsonSerializer.Serialize(PlatformPermissions.Normalize(permissions));
    }

    private static IEnumerable<string>? DeserializePermissions(string? permissionsJson)
    {
        if (string.IsNullOrWhiteSpace(permissionsJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(permissionsJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
