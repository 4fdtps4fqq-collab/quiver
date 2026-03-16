namespace KiteFlow.Services.Identity.Api.Configuration;

public sealed class SystemAdminBootstrapOptions
{
    public const string SectionName = "SystemAdminBootstrap";

    public bool Enabled { get; set; } = true;

    public string Email { get; set; } = "admin@quiver.local";

    public string Password { get; set; } = "Admin123!";
}
