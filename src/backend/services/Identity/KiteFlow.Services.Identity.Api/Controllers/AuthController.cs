using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Net;
using System.Net.Http.Json;
using KiteFlow.BuildingBlocks.Authentication;
using KiteFlow.Services.Identity.Api.Data;
using KiteFlow.Services.Identity.Api.Domain;
using KiteFlow.Services.Identity.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace KiteFlow.Services.Identity.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private const string InternalServiceKeyHeader = "X-KiteFlow-Internal-Key";
    private readonly IdentityDbContext _dbContext;
    private readonly JwtOptions _jwtOptions;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IIdentityEmailDeliveryService _emailDeliveryService;
    private readonly AuthenticationAuditService _auditService;

    public AuthController(
        IdentityDbContext dbContext,
        IOptions<JwtOptions> jwtOptions,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IIdentityEmailDeliveryService emailDeliveryService,
        AuthenticationAuditService auditService)
    {
        _dbContext = dbContext;
        _jwtOptions = jwtOptions.Value;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _emailDeliveryService = emailDeliveryService;
        _auditService = auditService;
    }

    [Authorize(Policy = "SystemAdminOnly")]
    [HttpPost("bootstrap-user")]
    public async Task<IActionResult> BootstrapUser([FromBody] BootstrapUserRequest request)
    {
        var email = NormalizeEmail(request.Email);

        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest("O e-mail é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            return BadRequest("A senha precisa ter pelo menos 8 caracteres.");
        }

        var exists = await _dbContext.UserAccounts.AnyAsync(x => x.Email == email);
        if (exists)
        {
            return Conflict("Ja existe uma conta com esse e-mail.");
        }

        var user = new UserAccount
        {
            Id = request.UserId ?? Guid.NewGuid(),
            SchoolId = request.SchoolId,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role,
            MustChangePassword = request.MustChangePassword
        };

        _dbContext.UserAccounts.Add(user);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(Me), new
        {
            userId = user.Id
        }, new
        {
            user.Id,
            user.SchoolId,
            user.Email,
            role = user.Role.ToString()
        });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _dbContext.UserAccounts.FirstOrDefaultAsync(x => x.Email == email && x.IsActive, cancellationToken);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password ?? string.Empty, user.PasswordHash))
        {
            await WriteAuditAsync(
                eventType: "auth.login",
                outcome: "Denied",
                email: email,
                metadata: new { reason = "invalid_credentials" },
                cancellationToken: cancellationToken);
            return Unauthorized("Credenciais invalidas.");
        }

        var schoolAccessError = await EnsureSchoolAllowsAccessAsync(user, cancellationToken);
        if (schoolAccessError is not null)
        {
            await WriteAuditAsync(
                eventType: "auth.login",
                outcome: "Denied",
                schoolId: user.SchoolId,
                userAccountId: user.Id,
                email: user.Email,
                metadata: new { reason = "school_unavailable" },
                cancellationToken: cancellationToken);
            return schoolAccessError;
        }

        user.LastLoginAtUtc = DateTime.UtcNow;
        var session = await IssueSessionAsync(user, request.DeviceName, cancellationToken: cancellationToken);
        await WriteAuditAsync(
            eventType: "auth.login",
            outcome: "Succeeded",
            schoolId: user.SchoolId,
            userAccountId: user.Id,
            email: user.Email,
            metadata: new { request.DeviceName },
            cancellationToken: cancellationToken);
        return Ok(session);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest("O refresh token é obrigatório.");
        }

        var tokenHash = HashToken(request.RefreshToken);
        var existingSession = await _dbContext.RefreshSessions
            .Include(x => x.UserAccount)
            .FirstOrDefaultAsync(x =>
                x.TokenHash == tokenHash &&
                x.RevokedAtUtc == null &&
                x.ExpiresAtUtc >= DateTime.UtcNow,
                cancellationToken);

        if (existingSession?.UserAccount is null || !existingSession.UserAccount.IsActive)
        {
            await WriteAuditAsync(
                eventType: "auth.refresh",
                outcome: "Denied",
                metadata: new { reason = "invalid_refresh_token" },
                cancellationToken: cancellationToken);
            return Unauthorized("Sua sessão de acesso expirou. Entre novamente.");
        }

        var schoolAccessError = await EnsureSchoolAllowsAccessAsync(existingSession.UserAccount, cancellationToken);
        if (schoolAccessError is not null)
        {
            await WriteAuditAsync(
                eventType: "auth.refresh",
                outcome: "Denied",
                schoolId: existingSession.UserAccount.SchoolId,
                userAccountId: existingSession.UserAccount.Id,
                email: existingSession.UserAccount.Email,
                metadata: new { reason = "school_unavailable" },
                cancellationToken: cancellationToken);
            return schoolAccessError;
        }

        existingSession.RevokedAtUtc = DateTime.UtcNow;
        var rotatedSession = await IssueSessionAsync(
            existingSession.UserAccount,
            request.DeviceName ?? existingSession.DeviceName,
            existingSession.Id,
            cancellationToken);

        await WriteAuditAsync(
            eventType: "auth.refresh",
            outcome: "Succeeded",
            schoolId: existingSession.UserAccount.SchoolId,
            userAccountId: existingSession.UserAccount.Id,
            email: existingSession.UserAccount.Email,
            metadata: new
            {
                requestedDeviceName = request.DeviceName,
                previousDeviceName = existingSession.DeviceName
            },
            cancellationToken: cancellationToken);

        return Ok(rotatedSession);
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Ok(new
            {
                loggedOutAtUtc = DateTime.UtcNow
            });
        }

        var tokenHash = HashToken(request.RefreshToken);
        var session = await _dbContext.RefreshSessions.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        if (session is not null && session.RevokedAtUtc is null)
        {
            session.RevokedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await WriteAuditAsync(
                eventType: "auth.logout",
                outcome: "Succeeded",
                schoolId: null,
                userAccountId: session.UserAccountId,
                metadata: new { sessionId = session.Id },
                cancellationToken: cancellationToken);
        }

        return Ok(new
        {
            loggedOutAtUtc = DateTime.UtcNow
        });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid.TryParse(userIdRaw, out var userId);
        var mustChangePassword = userId == Guid.Empty
            ? false
            : await _dbContext.UserAccounts.AsNoTracking()
                .Where(x => x.Id == userId)
                .Select(x => x.MustChangePassword)
                .FirstOrDefaultAsync(cancellationToken);

        return Ok(new
        {
            userId = userIdRaw,
            schoolId = User.FindFirstValue("school_id"),
            email = User.FindFirstValue(ClaimTypes.Email),
            role = User.FindFirstValue(ClaimTypes.Role),
            permissions = User.FindAll("permission").Select(x => x.Value).Distinct(StringComparer.OrdinalIgnoreCase),
            mustChangePassword
        });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdRaw, out var userId))
        {
            return Unauthorized();
        }

        var user = await _dbContext.UserAccounts.FirstOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken);
        if (user is null)
        {
            await WriteAuditAsync(
                eventType: "auth.change-password",
                outcome: "Denied",
                userAccountId: userId,
                metadata: new { reason = "user_not_found" },
                cancellationToken: cancellationToken);
            return NotFound("Conta não encontrada.");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword ?? string.Empty, user.PasswordHash))
        {
            await WriteAuditAsync(
                eventType: "auth.change-password",
                outcome: "Denied",
                schoolId: user.SchoolId,
                userAccountId: user.Id,
                email: user.Email,
                metadata: new { reason = "invalid_current_password" },
                cancellationToken: cancellationToken);
            return BadRequest("A senha atual informada não confere.");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            return BadRequest("A nova senha precisa ter pelo menos 8 caracteres.");
        }

        if (request.NewPassword == request.CurrentPassword)
        {
            return BadRequest("A nova senha precisa ser diferente da senha atual.");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.MustChangePassword = false;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var session = await IssueSessionAsync(
            user,
            request.DeviceName,
            cancellationToken: cancellationToken);

        await RevokeOtherSessionsAsync(user.Id, cancellationToken);
        await WriteAuditAsync(
            eventType: "auth.change-password",
            outcome: "Succeeded",
            schoolId: user.SchoolId,
            userAccountId: user.Id,
            email: user.Email,
            metadata: new { request.DeviceName },
            cancellationToken: cancellationToken);

        return Ok(session);
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _dbContext.UserAccounts.FirstOrDefaultAsync(x => x.Email == email && x.IsActive, cancellationToken);

        if (user is not null)
        {
            var resetToken = GenerateRefreshToken();
            var reset = new PasswordResetToken
            {
                UserAccountId = user.Id,
                TokenHash = HashToken(resetToken),
                ExpiresAtUtc = DateTime.UtcNow.AddHours(2),
                RequestedIpAddress = ResolveIpAddress(),
                RequestedUserAgent = ResolveUserAgent()
            };

            _dbContext.PasswordResetTokens.Add(reset);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var loginUrl = _configuration["IdentityEmailDelivery:PublicLoginUrl"] ?? "http://localhost:5174/login";
            var delivery = await _emailDeliveryService.SendPasswordResetAsync(
                new PasswordResetEmailMessage(
                    FullName: user.Email,
                    Email: user.Email,
                    ScopeLabel: user.SchoolId?.ToString() ?? "platform",
                    ResetUrl: $"{loginUrl}?reset={Uri.EscapeDataString(resetToken)}",
                    ExpiresAtUtc: reset.ExpiresAtUtc),
                cancellationToken);

            await WriteAuditAsync(
                eventType: "auth.forgot-password",
                outcome: "Succeeded",
                schoolId: user.SchoolId,
                userAccountId: user.Id,
                email: user.Email,
                metadata: new { delivery.Mode, delivery.OutboxFilePath },
                cancellationToken: cancellationToken);
        }
        else
        {
            await WriteAuditAsync(
                eventType: "auth.forgot-password",
                outcome: "Ignored",
                email: email,
                metadata: new { reason = "user_not_found_or_inactive" },
                cancellationToken: cancellationToken);
        }

        return Ok(new
        {
            requestedAtUtc = DateTime.UtcNow,
            message = "Se existir uma conta ativa com este e-mail, enviaremos as instruções de recuperação."
        });
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest("O token de recuperação é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            return BadRequest("A nova senha precisa ter pelo menos 8 caracteres.");
        }

        var tokenHash = HashToken(request.Token);
        var passwordReset = await _dbContext.PasswordResetTokens
            .Include(x => x.UserAccount)
            .FirstOrDefaultAsync(x =>
                x.TokenHash == tokenHash &&
                x.UsedAtUtc == null &&
                x.ExpiresAtUtc >= DateTime.UtcNow,
                cancellationToken);

        if (passwordReset?.UserAccount is null || !passwordReset.UserAccount.IsActive)
        {
            await WriteAuditAsync(
                eventType: "auth.reset-password",
                outcome: "Denied",
                metadata: new { reason = "invalid_or_expired_token" },
                cancellationToken: cancellationToken);
            return BadRequest("Este link de recuperação é inválido ou expirou.");
        }

        var user = passwordReset.UserAccount;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.MustChangePassword = false;
        passwordReset.UsedAtUtc = DateTime.UtcNow;

        await RevokeAllSessionsAsync(user.Id, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var session = await IssueSessionAsync(user, request.DeviceName, cancellationToken: cancellationToken);
        await WriteAuditAsync(
            eventType: "auth.reset-password",
            outcome: "Succeeded",
            schoolId: user.SchoolId,
            userAccountId: user.Id,
            email: user.Email,
            metadata: new { request.DeviceName },
            cancellationToken: cancellationToken);

        return Ok(session);
    }

    private async Task<LoginResponse> IssueSessionAsync(
        UserAccount user,
        string? deviceName,
        Guid? revokedSessionId = null,
        CancellationToken cancellationToken = default)
    {
        if (revokedSessionId.HasValue)
        {
            var priorSession = await _dbContext.RefreshSessions.FirstOrDefaultAsync(x => x.Id == revokedSessionId.Value, cancellationToken);
            if (priorSession is not null && priorSession.RevokedAtUtc is null)
            {
                priorSession.RevokedAtUtc = DateTime.UtcNow;
            }
        }

        var refreshToken = GenerateRefreshToken();
        var refreshExpiresAtUtc = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays <= 0 ? 14 : _jwtOptions.RefreshTokenDays);

        var refreshSession = new RefreshSession
        {
            UserAccountId = user.Id,
            TokenHash = HashToken(refreshToken),
            DeviceName = NormalizeNullable(deviceName),
            ExpiresAtUtc = refreshExpiresAtUtc
        };

        _dbContext.RefreshSessions.Add(refreshSession);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new LoginResponse(
            Token: CreateToken(user),
            RefreshToken: refreshToken,
            RefreshTokenExpiresAtUtc: refreshExpiresAtUtc,
            UserId: user.Id,
            SchoolId: user.SchoolId,
            Email: user.Email,
            Role: user.Role.ToString(),
            Permissions: user.GetEffectivePermissions(),
            MustChangePassword: user.MustChangePassword);
    }

    private async Task RevokeAllSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var sessions = await _dbContext.RefreshSessions
            .Where(x => x.UserAccountId == userId && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            session.RevokedAtUtc = DateTime.UtcNow;
        }
    }

    private async Task RevokeOtherSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var sessions = await _dbContext.RefreshSessions
            .Where(x => x.UserAccountId == userId && x.RevokedAtUtc == null)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions.Skip(1))
        {
            session.RevokedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private string CreateToken(UserAccount user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("must_change_password", user.MustChangePassword ? "true" : "false")
        };

        if (user.SchoolId.HasValue)
        {
            claims.Add(new Claim("school_id", user.SchoolId.Value.ToString()));
        }

        claims.AddRange(user.GetEffectivePermissions().Select(static permission => new Claim("permission", permission)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_jwtOptions.AccessTokenHours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string NormalizeEmail(string? email)
        => (email ?? string.Empty).Trim().ToLowerInvariant();

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string GenerateRefreshToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private async Task<IActionResult?> EnsureSchoolAllowsAccessAsync(UserAccount user, CancellationToken cancellationToken)
    {
        if (!user.SchoolId.HasValue)
        {
            return null;
        }

        var client = _httpClientFactory.CreateClient("schools");
        var sharedKey = _configuration["InternalServiceAuth:SharedKey"];

        if (client.BaseAddress is null || string.IsNullOrWhiteSpace(sharedKey))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Não foi possível validar o status da escola agora.");
        }

        client.DefaultRequestHeaders.Remove(InternalServiceKeyHeader);
        client.DefaultRequestHeaders.Add(InternalServiceKeyHeader, sharedKey);

        var response = await client.GetAsync($"/api/v1/internal/schools/{user.SchoolId.Value}/access", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return StatusCode(StatusCodes.Status403Forbidden, "A escola vinculada a esta conta não está mais disponível.");
        }

        if (!response.IsSuccessStatusCode)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Não foi possível validar o status da escola agora.");
        }

        var school = await response.Content.ReadFromJsonAsync<SchoolAccessResponse>(cancellationToken: cancellationToken);
        if (school is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Não foi possível validar o status da escola agora.");
        }

        if (school.IsAccessAllowed)
        {
            return null;
        }

        var message = school.Status.Equals("Suspended", StringComparison.OrdinalIgnoreCase)
            ? "A escola vinculada a esta conta está inativa no momento. Entre em contato com a administração da plataforma."
            : "A escola vinculada a esta conta ainda não está liberada para operar.";

        return StatusCode(StatusCodes.Status403Forbidden, message);
    }

    private async Task WriteAuditAsync(
        string eventType,
        string outcome,
        Guid? schoolId = null,
        Guid? userAccountId = null,
        Guid? targetUserAccountId = null,
        string? email = null,
        object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        await _auditService.WriteAsync(
            eventType: eventType,
            outcome: outcome,
            schoolId: schoolId,
            userAccountId: userAccountId,
            targetUserAccountId: targetUserAccountId,
            email: email,
            ipAddress: ResolveIpAddress(),
            userAgent: ResolveUserAgent(),
            metadata: metadata,
            cancellationToken: cancellationToken);
    }

    private string? ResolveIpAddress()
        => HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? ResolveUserAgent()
        => Request.Headers.UserAgent.ToString();

    public sealed record BootstrapUserRequest(
        Guid? UserId,
        Guid? SchoolId,
        string Email,
        string Password,
        PlatformRole Role,
        bool MustChangePassword = false);

    public sealed record LoginRequest(string Email, string Password, string? DeviceName = null);

    public sealed record RefreshTokenRequest(string RefreshToken, string? DeviceName = null);

    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword, string? DeviceName = null);

    public sealed record ForgotPasswordRequest(string Email);

    public sealed record ResetPasswordRequest(string Token, string NewPassword, string? DeviceName = null);

    public sealed record LoginResponse(
        string Token,
        string RefreshToken,
        DateTime RefreshTokenExpiresAtUtc,
        Guid UserId,
        Guid? SchoolId,
        string Email,
        string Role,
        IReadOnlyCollection<string> Permissions,
        bool MustChangePassword);

    private sealed record SchoolAccessResponse(
        Guid Id,
        string DisplayName,
        string Status,
        bool IsAccessAllowed);
}
