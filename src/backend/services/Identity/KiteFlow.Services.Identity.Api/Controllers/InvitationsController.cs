using System.Security.Cryptography;
using System.Text;
using KiteFlow.BuildingBlocks.MultiTenancy;
using KiteFlow.Services.Identity.Api.Data;
using KiteFlow.Services.Identity.Api.Domain;
using KiteFlow.Services.Identity.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Identity.Api.Controllers;

[ApiController]
[Route("api/v1/invitations")]
public sealed class InvitationsController : ControllerBase
{
    private readonly IdentityDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;
    private readonly IIdentityEmailDeliveryService _emailDeliveryService;
    private readonly AuthenticationAuditService _auditService;
    private readonly IConfiguration _configuration;

    public InvitationsController(
        IdentityDbContext dbContext,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        IIdentityEmailDeliveryService emailDeliveryService,
        AuthenticationAuditService auditService,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
        _emailDeliveryService = emailDeliveryService;
        _auditService = auditService;
        _configuration = configuration;
    }

    [Authorize(Policy = "SchoolManagementAccess")]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var items = await _dbContext.UserInvitations
            .Where(x => x.SchoolId == schoolId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.Email,
                x.FullName,
                x.Phone,
                role = x.Role.ToString(),
                x.ExpiresAtUtc,
                x.CreatedAtUtc,
                x.AcceptedAtUtc,
                x.CancelledAtUtc,
                status = x.AcceptedAtUtc.HasValue
                    ? "Accepted"
                    : x.CancelledAtUtc.HasValue
                        ? "Cancelled"
                        : x.ExpiresAtUtc < DateTime.UtcNow
                            ? "Expired"
                            : "Pending"
            })
            .ToListAsync();

        return Ok(items);
    }

    [Authorize(Policy = "SchoolManagementAccess")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInvitationRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var email = NormalizeEmail(request.Email);
        var fullName = (request.FullName ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest("O e-mail do convite é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return BadRequest("O nome completo da pessoa convidada é obrigatório.");
        }

        if (request.Role == PlatformRole.SystemAdmin || request.Role == PlatformRole.Owner)
        {
            return BadRequest("Não é permitido convidar esse papel dentro da gestão da escola.");
        }

        var alreadyInvited = await _dbContext.UserInvitations.AnyAsync(x =>
            x.SchoolId == schoolId &&
            x.Email == email &&
            x.AcceptedAtUtc == null &&
            x.CancelledAtUtc == null &&
            x.ExpiresAtUtc >= DateTime.UtcNow);

        if (alreadyInvited)
        {
            return Conflict("Ja existe um convite pendente para esse e-mail.");
        }

        var accountExists = await _dbContext.UserAccounts.AnyAsync(x => x.Email == email);
        if (accountExists)
        {
            return Conflict("Ja existe uma conta cadastrada com esse e-mail.");
        }

        if (_currentUser.UserId is null)
        {
            return Unauthorized();
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');

        var invitation = new UserInvitation
        {
            SchoolId = schoolId,
            Email = email,
            FullName = fullName,
            Phone = NormalizeNullable(request.Phone),
            Role = request.Role,
            TokenHash = HashToken(token),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(request.ExpiresInDays is > 0 and <= 30 ? request.ExpiresInDays : 7),
            CreatedByUserId = _currentUser.UserId.Value
        };

        _dbContext.UserInvitations.Add(invitation);
        await _dbContext.SaveChangesAsync();

        var loginUrl = _configuration["IdentityEmailDelivery:PublicLoginUrl"] ?? "http://localhost:5174/login";
        var inviteUrl = $"{loginUrl}?invite={Uri.EscapeDataString(token)}";
        var delivery = await _emailDeliveryService.SendInvitationAsync(
            new InvitationEmailMessage(
                FullName: invitation.FullName,
                Email: invitation.Email,
                SchoolDisplayName: request.SchoolDisplayName ?? request.SchoolSlug ?? "Escola Quiver",
                SchoolSlug: request.SchoolSlug ?? "school",
                RoleLabel: invitation.Role.ToString(),
                InviteUrl: inviteUrl,
                ExpiresAtUtc: invitation.ExpiresAtUtc),
            CancellationToken.None);

        await _auditService.WriteAsync(
            eventType: "identity.invitation.create",
            outcome: "Succeeded",
            schoolId: schoolId,
            userAccountId: _currentUser.UserId,
            email: invitation.Email,
            metadata: new
            {
                invitation.Id,
                role = invitation.Role.ToString(),
                delivery.Mode,
                delivery.OutboxFilePath
            });

        return Ok(new
        {
            invitation.Id,
            invitation.Email,
            invitation.FullName,
            phone = invitation.Phone,
            role = invitation.Role.ToString(),
            invitation.ExpiresAtUtc,
            invitation.CreatedAtUtc,
            status = "Pending",
            deliveryMode = delivery.Mode,
            temporaryLink = delivery.Mode.Equals("File", StringComparison.OrdinalIgnoreCase) ? inviteUrl : null,
            outboxFilePath = delivery.OutboxFilePath
        });
    }

    [AllowAnonymous]
    [HttpGet("preview")]
    public async Task<IActionResult> Preview([FromQuery] string token)
    {
        var invitation = await FindInvitationAsync(token);
        if (invitation is null)
        {
            return NotFound("Convite não encontrado.");
        }

        if (invitation.CancelledAtUtc.HasValue)
        {
            return BadRequest("Este convite foi cancelado.");
        }

        if (invitation.AcceptedAtUtc.HasValue)
        {
            return BadRequest("Este convite ja foi utilizado.");
        }

        if (invitation.ExpiresAtUtc < DateTime.UtcNow)
        {
            return BadRequest("Este convite expirou.");
        }

        return Ok(new
        {
            invitation.Id,
            invitation.Email,
            invitation.FullName,
            invitation.Phone,
            role = invitation.Role.ToString(),
            invitation.ExpiresAtUtc,
            status = "Pending"
        });
    }

    [AllowAnonymous]
    [HttpPost("accept")]
    public async Task<IActionResult> Accept([FromBody] AcceptInvitationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            return BadRequest("A senha precisa ter pelo menos 8 caracteres.");
        }

        var invitation = await FindInvitationAsync(request.Token);
        if (invitation is null)
        {
            return NotFound("Convite não encontrado.");
        }

        if (invitation.CancelledAtUtc.HasValue)
        {
            return BadRequest("Este convite foi cancelado.");
        }

        if (invitation.AcceptedAtUtc.HasValue)
        {
            return BadRequest("Este convite ja foi utilizado.");
        }

        if (invitation.ExpiresAtUtc < DateTime.UtcNow)
        {
            return BadRequest("Este convite expirou.");
        }

        var emailExists = await _dbContext.UserAccounts.AnyAsync(x => x.Email == invitation.Email);
        if (emailExists)
        {
            return Conflict("Ja existe uma conta cadastrada com esse e-mail.");
        }

        var user = new UserAccount
        {
            SchoolId = invitation.SchoolId,
            Email = invitation.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = invitation.Role,
            MustChangePassword = false,
            IsActive = true
        };

        invitation.AcceptedAtUtc = DateTime.UtcNow;
        invitation.AcceptedUserId = user.Id;

        _dbContext.UserAccounts.Add(user);
        await _dbContext.SaveChangesAsync();

        await _auditService.WriteAsync(
            eventType: "identity.invitation.accept",
            outcome: "Succeeded",
            schoolId: invitation.SchoolId,
            userAccountId: user.Id,
            email: invitation.Email,
            metadata: new
            {
                invitation.Id,
                role = invitation.Role.ToString()
            });

        return Ok(new
        {
            invitationId = invitation.Id,
            userId = user.Id,
            invitation.SchoolId,
            invitation.Email,
            invitation.FullName,
            invitation.Phone,
            role = invitation.Role.ToString()
        });
    }

    [Authorize(Policy = "SchoolManagementAccess")]
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var invitation = await _dbContext.UserInvitations.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId);
        if (invitation is null)
        {
            return NotFound();
        }

        if (invitation.AcceptedAtUtc.HasValue)
        {
            return BadRequest("Não é possível cancelar um convite que já foi aceito.");
        }

        if (invitation.CancelledAtUtc.HasValue)
        {
            return Ok();
        }

        invitation.CancelledAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        await _auditService.WriteAsync(
            eventType: "identity.invitation.cancel",
            outcome: "Succeeded",
            schoolId: schoolId,
            userAccountId: _currentUser.UserId,
            email: invitation.Email,
            metadata: new { invitation.Id });
        return Ok();
    }

    private async Task<UserInvitation?> FindInvitationAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var hash = HashToken(token.Trim());
        return await _dbContext.UserInvitations.FirstOrDefaultAsync(x => x.TokenHash == hash);
    }

    private static string NormalizeEmail(string? email)
        => (email ?? string.Empty).Trim().ToLowerInvariant();

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    public sealed record CreateInvitationRequest(
        string Email,
        string FullName,
        PlatformRole Role,
        string? Phone,
        int ExpiresInDays,
        string? SchoolDisplayName = null,
        string? SchoolSlug = null);

    public sealed record AcceptInvitationRequest(string Token, string Password);
}
