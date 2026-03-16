using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace KiteFlow.BuildingBlocks.MultiTenancy;

public interface ICurrentTenant
{
    Guid? SchoolId { get; }

    void EnsureTenant();
}

public interface ICurrentUser
{
    Guid? UserId { get; }

    string? Email { get; }

    string? Role { get; }
}

internal sealed class CurrentTenant : ICurrentTenant
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentTenant(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? SchoolId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User.FindFirstValue("school_id");
            return Guid.TryParse(value, out var schoolId) ? schoolId : null;
        }
    }

    public void EnsureTenant()
    {
        if (SchoolId is null)
        {
            throw new InvalidOperationException("Current request does not contain a school_id claim.");
        }
    }
}

internal sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var userId) ? userId : null;
        }
    }

    public string? Email => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email);

    public string? Role => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role);
}
