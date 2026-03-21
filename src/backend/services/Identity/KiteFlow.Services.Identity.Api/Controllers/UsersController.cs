using KiteFlow.BuildingBlocks.MultiTenancy;
using KiteFlow.Services.Identity.Api.Data;
using KiteFlow.Services.Identity.Api.Domain;
using KiteFlow.Services.Identity.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Identity.Api.Controllers;

[ApiController]
[Authorize(Policy = "SchoolManagementAccess")]
[Route("api/v1/users")]
public sealed class UsersController : ControllerBase
{
    private readonly IdentityDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;
    private readonly IIdentityEmailDeliveryService _emailDeliveryService;
    private readonly AuthenticationAuditService _auditService;

    public UsersController(
        IdentityDbContext dbContext,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        IIdentityEmailDeliveryService emailDeliveryService,
        AuthenticationAuditService auditService)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
        _emailDeliveryService = emailDeliveryService;
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = false)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var query = _dbContext.UserAccounts.Where(x => x.SchoolId == schoolId);
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        var items = await query
            .OrderBy(x => x.Role)
            .ThenBy(x => x.Email)
            .ToListAsync();

        return Ok(items.Select(x => new
        {
            x.Id,
            x.Email,
            role = x.Role.ToString(),
            permissions = x.GetEffectivePermissions(),
            x.IsActive,
            x.MustChangePassword,
            x.CreatedAtUtc,
            x.LastLoginAtUtc
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest("O e-mail é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            return BadRequest("A senha precisa ter pelo menos 8 caracteres.");
        }

        if (request.Role == PlatformRole.SystemAdmin || request.Role == PlatformRole.Owner)
        {
            return BadRequest("Não é permitido criar esse papel dentro da gestão da escola.");
        }

        var exists = await _dbContext.UserAccounts.AnyAsync(x => x.Email == email);
        if (exists)
        {
            return Conflict("Ja existe uma conta cadastrada com esse e-mail.");
        }

        var user = new UserAccount
        {
            SchoolId = schoolId,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role,
            MustChangePassword = request.MustChangePassword,
            IsActive = request.IsActive
        };
        user.SetPermissions(request.Permissions);

        _dbContext.UserAccounts.Add(user);
        await _dbContext.SaveChangesAsync();

        IdentityEmailDeliveryResult? delivery = null;
        if (request.DeliverTemporaryPasswordByEmail)
        {
            delivery = await _emailDeliveryService.SendTemporaryPasswordAsync(
                new TemporaryPasswordEmailMessage(
                    FullName: string.IsNullOrWhiteSpace(request.FullName) ? user.Email : request.FullName.Trim(),
                    Email: user.Email,
                    ScopeLabel: string.IsNullOrWhiteSpace(request.ScopeLabel) ? schoolId.ToString() : request.ScopeLabel.Trim(),
                    TemporaryPassword: request.Password),
                CancellationToken.None);
        }

        await _auditService.WriteAsync(
            eventType: "identity.user.create",
            outcome: "Succeeded",
            schoolId: schoolId,
            userAccountId: _currentUser.UserId,
            targetUserAccountId: user.Id,
            email: user.Email,
            metadata: new
            {
                role = user.Role.ToString(),
                user.IsActive,
                user.MustChangePassword,
                deliveredByEmail = request.DeliverTemporaryPasswordByEmail,
                deliveryMode = delivery?.Mode,
                delivery?.OutboxFilePath
            });

        return CreatedAtAction(nameof(GetAll), new { id = user.Id }, new
        {
            user.Id,
            user.Email,
            role = user.Role.ToString(),
            permissions = user.GetEffectivePermissions(),
            user.IsActive,
            user.MustChangePassword,
            deliveryMode = delivery?.Mode,
            outboxFilePath = delivery?.OutboxFilePath
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        if (request.Role == PlatformRole.SystemAdmin || request.Role == PlatformRole.Owner)
        {
            return BadRequest("Não é permitido atribuir esse papel dentro da gestão da escola.");
        }

        var user = await _dbContext.UserAccounts.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
        if (user is null)
        {
            return NotFound();
        }

        if (_currentUser.UserId == user.Id && !request.IsActive)
        {
            return BadRequest("Você não pode desativar a própria conta.");
        }

        user.Role = request.Role;
        var wasActive = user.IsActive;
        user.IsActive = request.IsActive;
        user.MustChangePassword = request.MustChangePassword;
        user.SetPermissions(request.Permissions);

        if (wasActive && !user.IsActive)
        {
            var sessions = await _dbContext.RefreshSessions
                .Where(x => x.UserAccountId == user.Id && x.RevokedAtUtc == null)
                .ToListAsync();

            foreach (var session in sessions)
            {
                session.RevokedAtUtc = DateTime.UtcNow;
            }
        }

        await _dbContext.SaveChangesAsync();

        await _auditService.WriteAsync(
            eventType: wasActive != request.IsActive ? "identity.user.activation" : "identity.user.update",
            outcome: "Succeeded",
            schoolId: schoolId,
            userAccountId: _currentUser.UserId,
            targetUserAccountId: user.Id,
            email: user.Email,
            metadata: new
            {
                role = user.Role.ToString(),
                previousActive = wasActive,
                currentActive = user.IsActive,
                user.MustChangePassword
            });
        return Ok();
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        if (_currentUser.UserId == id)
        {
            return BadRequest("Use a troca de senha pessoal para alterar a própria senha.");
        }

        if (string.IsNullOrWhiteSpace(request.TemporaryPassword) || request.TemporaryPassword.Length < 8)
        {
            return BadRequest("A senha temporaria precisa ter pelo menos 8 caracteres.");
        }

        var user = await _dbContext.UserAccounts.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
        if (user is null)
        {
            return NotFound();
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        if (!string.IsNullOrWhiteSpace(normalizedEmail) &&
            !string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            var emailInUse = await _dbContext.UserAccounts.AnyAsync(
                x => x.Id != user.Id && x.Email == normalizedEmail,
                cancellationToken);

            if (emailInUse)
            {
                return Conflict("Já existe uma conta cadastrada com esse e-mail.");
            }

            user.Email = normalizedEmail;
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.TemporaryPassword);
        user.MustChangePassword = request.MustChangePassword;
        user.IsActive = true;

        var sessions = await _dbContext.RefreshSessions
            .Where(x => x.UserAccountId == user.Id && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            session.RevokedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();

        IdentityEmailDeliveryResult? delivery = null;
        if (request.DeliverByEmail)
        {
            delivery = await _emailDeliveryService.SendTemporaryPasswordAsync(
                new TemporaryPasswordEmailMessage(
                    FullName: string.IsNullOrWhiteSpace(request.FullName) ? user.Email : request.FullName.Trim(),
                    Email: user.Email,
                    ScopeLabel: string.IsNullOrWhiteSpace(request.ScopeLabel) ? schoolId.ToString() : request.ScopeLabel.Trim(),
                    TemporaryPassword: request.TemporaryPassword),
                cancellationToken);
        }

        await _auditService.WriteAsync(
            eventType: "identity.user.reset-password",
            outcome: "Succeeded",
            schoolId: schoolId,
            userAccountId: _currentUser.UserId,
            targetUserAccountId: user.Id,
            email: user.Email,
            metadata: new
            {
                deliveredByEmail = request.DeliverByEmail,
                deliveryMode = delivery?.Mode,
                delivery?.OutboxFilePath
            },
            cancellationToken: cancellationToken);

        return Ok(new
        {
            resetAtUtc = DateTime.UtcNow,
            userId = user.Id,
            user.Email,
            user.MustChangePassword,
            deliveryMode = delivery?.Mode,
            outboxFilePath = delivery?.OutboxFilePath
        });
    }

    public sealed record CreateUserRequest(
        string Email,
        string Password,
        PlatformRole Role,
        IReadOnlyCollection<string>? Permissions,
        bool MustChangePassword,
        bool IsActive,
        bool DeliverTemporaryPasswordByEmail = false,
        string? FullName = null,
        string? ScopeLabel = null);

    public sealed record UpdateUserRequest(
        PlatformRole Role,
        IReadOnlyCollection<string>? Permissions,
        bool MustChangePassword,
        bool IsActive);

    public sealed record ResetPasswordRequest(
        string TemporaryPassword,
        bool MustChangePassword = true,
        bool DeliverByEmail = true,
        string? Email = null,
        string? FullName = null,
        string? ScopeLabel = null);

    private static string NormalizeEmail(string? email)
        => (email ?? string.Empty).Trim().ToLowerInvariant();
}
