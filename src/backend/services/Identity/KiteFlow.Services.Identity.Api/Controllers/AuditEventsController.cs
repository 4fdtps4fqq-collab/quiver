using System.Text.Json;
using KiteFlow.BuildingBlocks.MultiTenancy;
using KiteFlow.Services.Identity.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Identity.Api.Controllers;

[ApiController]
[Route("api/v1/audit-events")]
public sealed class AuditEventsController : ControllerBase
{
    private readonly IdentityDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;

    public AuditEventsController(IdentityDbContext dbContext, ICurrentTenant currentTenant)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
    }

    [Authorize(Policy = "AuthenticatedUsers")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int take = 50)
    {
        take = Math.Clamp(take, 1, 200);
        var query = _dbContext.AuthenticationAuditEvents.AsNoTracking();

        if (!User.IsInRole("SystemAdmin"))
        {
            _currentTenant.EnsureTenant();
            var schoolId = _currentTenant.SchoolId!.Value;
            query = query.Where(x => x.SchoolId == schoolId);
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(take)
            .Select(x => new
            {
                x.Id,
                x.SchoolId,
                x.UserAccountId,
                x.TargetUserAccountId,
                x.EventType,
                x.Outcome,
                x.Email,
                x.IpAddress,
                x.UserAgent,
                x.MetadataJson,
                x.CreatedAtUtc
            })
            .ToListAsync();

        return Ok(items.Select(item => new
        {
            item.Id,
            item.SchoolId,
            item.UserAccountId,
            item.TargetUserAccountId,
            item.EventType,
            item.Outcome,
            item.Email,
            item.IpAddress,
            item.UserAgent,
            metadata = TryParseJson(item.MetadataJson),
            item.CreatedAtUtc
        }));
    }

    private static object? TryParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch (JsonException)
        {
            return json;
        }
    }
}
