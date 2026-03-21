namespace KiteFlow.Gateway.Configuration;

public sealed class OwnerCredentialDeliveryOptions
{
    public const string SectionName = "OwnerCredentialDelivery";

    public string Mode { get; set; } = "File";

    public string FromEmail { get; set; } = "noreply@quiver.local";

    public string FromName { get; set; } = "Quiver";

    public string? OutboxDirectory { get; set; } = "temp/email-outbox";

    public string? SmtpHost { get; set; }

    public int SmtpPort { get; set; } = 587;

    public string? SmtpUsername { get; set; }

    public string? SmtpPassword { get; set; }

    public bool SmtpUseSsl { get; set; } = true;

    public string PublicLoginUrl { get; set; } = "http://localhost:5174/login";
}
