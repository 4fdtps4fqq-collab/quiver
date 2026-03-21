using System.Text.Json;
using KiteFlow.Services.Reporting.Api.Data;
using KiteFlow.Services.Reporting.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace KiteFlow.Services.Reporting.Api.Services;

public sealed class ReportingSnapshotService
{
    private const int SnapshotVersion = 1;
    private static readonly DateTime DefaultWindowStartUtc = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DefaultWindowEndUtc = new(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ReportingDbContext _dbContext;
    private readonly IMemoryCache _memoryCache;
    private readonly IConfiguration _configuration;

    public ReportingSnapshotService(
        ReportingDbContext dbContext,
        IMemoryCache memoryCache,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _memoryCache = memoryCache;
        _configuration = configuration;
    }

    public async Task<JsonElement> GetOrCreateAsync(
        Guid schoolId,
        string reportName,
        DateTime? fromUtc,
        DateTime? toUtc,
        Func<CancellationToken, Task<object>> factory,
        CancellationToken cancellationToken)
    {
        var windowStartUtc = NormalizeWindowStart(fromUtc);
        var windowEndUtc = NormalizeWindowEnd(toUtc);
        var cacheKey = $"{reportName}:{schoolId}:{windowStartUtc:O}:{windowEndUtc:O}:{SnapshotVersion}";

        if (_memoryCache.TryGetValue(cacheKey, out JsonElement cached))
        {
            return cached;
        }

        var now = DateTime.UtcNow;
        var snapshot = await _dbContext.ReportSnapshots.AsNoTracking().FirstOrDefaultAsync(
            x => x.SchoolId == schoolId &&
                 x.ReportName == reportName &&
                 x.WindowStartUtc == windowStartUtc &&
                 x.WindowEndUtc == windowEndUtc &&
                 x.SnapshotVersion == SnapshotVersion &&
                 x.ExpiresAtUtc > now,
            cancellationToken);

        if (snapshot is not null)
        {
            var snapshotPayload = ParseJson(snapshot.PayloadJson);
            Cache(cacheKey, snapshotPayload);
            return snapshotPayload;
        }

        var payload = await factory(cancellationToken);
        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadElement = ParseJson(payloadJson);
        var ttlSeconds = HasServiceErrors(payloadElement)
            ? _configuration.GetValue("Reporting:DegradedSnapshotTtlSeconds", 60)
            : _configuration.GetValue("Reporting:SnapshotTtlSeconds", 300);

        var trackedSnapshot = await _dbContext.ReportSnapshots.FirstOrDefaultAsync(
            x => x.SchoolId == schoolId &&
                 x.ReportName == reportName &&
                 x.WindowStartUtc == windowStartUtc &&
                 x.WindowEndUtc == windowEndUtc,
            cancellationToken);

        if (trackedSnapshot is null)
        {
            trackedSnapshot = new ReportSnapshot
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolId,
                ReportName = reportName,
                WindowStartUtc = windowStartUtc,
                WindowEndUtc = windowEndUtc
            };
            _dbContext.ReportSnapshots.Add(trackedSnapshot);
        }

        trackedSnapshot.SnapshotVersion = SnapshotVersion;
        trackedSnapshot.PayloadJson = payloadJson;
        trackedSnapshot.GeneratedAtUtc = now;
        trackedSnapshot.ExpiresAtUtc = now.AddSeconds(Math.Max(ttlSeconds, 15));

        await _dbContext.SaveChangesAsync(cancellationToken);

        Cache(cacheKey, payloadElement);
        return payloadElement;
    }

    private void Cache(string cacheKey, JsonElement payload)
    {
        var cacheSeconds = _configuration.GetValue("Reporting:MemoryCacheSeconds", 30);
        _memoryCache.Set(cacheKey, payload, TimeSpan.FromSeconds(Math.Max(cacheSeconds, 5)));
    }

    private static DateTime NormalizeWindowStart(DateTime? value) => (value ?? DefaultWindowStartUtc).ToUniversalTime();

    private static DateTime NormalizeWindowEnd(DateTime? value) => (value ?? DefaultWindowEndUtc).ToUniversalTime();

    private static JsonElement ParseJson(string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);
        return document.RootElement.Clone();
    }

    private static bool HasServiceErrors(JsonElement payload)
    {
        return payload.TryGetProperty("serviceErrors", out var serviceErrors) &&
               serviceErrors.ValueKind == JsonValueKind.Array &&
               serviceErrors.GetArrayLength() > 0;
    }
}
