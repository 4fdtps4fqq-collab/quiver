using KiteFlow.Services.Identity.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Identity.Api.Controllers;

[ApiController]
[Authorize(Policy = "SystemAdminOnly")]
[Route("api/v1/system/tenants")]
public sealed class SystemTenantsController : ControllerBase
{
    private readonly IdentityDbContext _dbContext;

    public SystemTenantsController(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpDelete("{schoolId:guid}")]
    public async Task<IActionResult> DeleteTenant(Guid schoolId)
    {
        var userIds = await _dbContext.UserAccounts
            .Where(x => x.SchoolId == schoolId)
            .Select(x => x.Id)
            .ToListAsync();

        if (userIds.Count > 0)
        {
            var refreshSessions = await _dbContext.RefreshSessions
                .Where(x => userIds.Contains(x.UserAccountId))
                .ToListAsync();

            if (refreshSessions.Count > 0)
            {
                _dbContext.RefreshSessions.RemoveRange(refreshSessions);
            }
        }

        var invitations = await _dbContext.UserInvitations
            .Where(x => x.SchoolId == schoolId)
            .ToListAsync();

        var accounts = await _dbContext.UserAccounts
            .Where(x => x.SchoolId == schoolId)
            .ToListAsync();

        if (invitations.Count > 0)
        {
            _dbContext.UserInvitations.RemoveRange(invitations);
        }

        if (accounts.Count > 0)
        {
            _dbContext.UserAccounts.RemoveRange(accounts);
        }

        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            deletedAtUtc = DateTime.UtcNow,
            schoolId,
            deletedUsers = accounts.Count,
            deletedInvitations = invitations.Count
        });
    }
}
