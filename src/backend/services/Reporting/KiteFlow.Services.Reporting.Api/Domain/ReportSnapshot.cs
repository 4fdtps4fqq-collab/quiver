namespace KiteFlow.Services.Reporting.Api.Domain;

public sealed class ReportSnapshot
{
    public Guid Id { get; set; }

    public Guid SchoolId { get; set; }

    public string ReportName { get; set; } = string.Empty;

    public DateTime WindowStartUtc { get; set; }

    public DateTime WindowEndUtc { get; set; }

    public int SnapshotVersion { get; set; }

    public string PayloadJson { get; set; } = "{}";

    public DateTime GeneratedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }
}
