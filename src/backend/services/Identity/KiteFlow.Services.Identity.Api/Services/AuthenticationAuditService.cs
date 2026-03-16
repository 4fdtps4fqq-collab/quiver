using System.Text.Json;
using KiteFlow.Services.Identity.Api.Data;
using KiteFlow.Services.Identity.Api.Domain;

namespace KiteFlow.Services.Identity.Api.Services;

public sealed class AuthenticationAuditService
{
    private readonly IdentityDbContext _dbContext;

    public AuthenticationAuditService(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task WriteAsync(
        string eventType,
        string outcome,
        Guid? schoolId = null,
        Guid? userAccountId = null,
        Guid? targetUserAccountId = null,
        string? email = null,
        string? ipAddress = null,
        string? userAgent = null,
        object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var auditEvent = new AuthenticationAuditEvent
        {
            SchoolId = schoolId,
            UserAccountId = userAccountId,
            TargetUserAccountId = targetUserAccountId,
            EventType = eventType,
            Outcome = outcome,
            Email = email,
            IpAddress = Normalize(ipAddress),
            UserAgent = Normalize(userAgent),
            MetadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata)
        };

        _dbContext.AuthenticationAuditEvents.Add(auditEvent);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
