using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KiteFlow.Gateway.Controllers;

[ApiController]
[Authorize(Policy = "SchoolManagementAccess")]
[Route("api/v1/school-users")]
public sealed class SchoolUsersController : ControllerBase
{
    private const string InternalServiceKeyHeader = "X-KiteFlow-Internal-Key";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public SchoolUsersController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var identityUsers = await GetJsonAsync("identity", "/api/v1/users", cancellationToken);
        if (identityUsers is null)
        {
            return StatusCode(StatusCodes.Status502BadGateway, "O serviço de identidade não respondeu.");
        }

        var schoolProfiles = await GetJsonAsync("schools", "/api/v1/users", cancellationToken);
        if (schoolProfiles is null)
        {
            return StatusCode(StatusCodes.Status502BadGateway, "O serviço de escolas não respondeu.");
        }

        var identityMap = identityUsers.Value
            .EnumerateArray()
            .ToDictionary(
                item => item.GetProperty("id").GetGuid(),
                item => item);

        var merged = new List<SchoolUserListItem>();
        foreach (var profile in schoolProfiles.Value.EnumerateArray())
        {
            var identityUserId = profile.GetProperty("identityUserId").GetGuid();
            identityMap.TryGetValue(identityUserId, out var account);

            merged.Add(new SchoolUserListItem(
                ProfileId: profile.GetProperty("id").GetGuid(),
                IdentityUserId: identityUserId,
                FullName: profile.GetProperty("fullName").GetString() ?? string.Empty,
                Phone: profile.TryGetProperty("phone", out var phone) ? phone.GetString() : null,
                SalaryAmount: profile.TryGetProperty("salaryAmount", out var salaryAmount) && salaryAmount.ValueKind != JsonValueKind.Null
                    ? salaryAmount.GetDecimal()
                    : null,
                AvatarUrl: profile.TryGetProperty("avatarUrl", out var avatar) ? avatar.GetString() : null,
                ProfileIsActive: profile.GetProperty("isActive").GetBoolean(),
                Email: account.ValueKind == JsonValueKind.Undefined ? null : account.GetProperty("email").GetString(),
                Role: account.ValueKind == JsonValueKind.Undefined ? null : account.GetProperty("role").GetString(),
                Permissions: account.ValueKind == JsonValueKind.Undefined ||
                    !account.TryGetProperty("permissions", out var permissions) ||
                    permissions.ValueKind != JsonValueKind.Array
                    ? Array.Empty<string>()
                    : permissions.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).ToArray(),
                IsActive: account.ValueKind == JsonValueKind.Undefined ? profile.GetProperty("isActive").GetBoolean() : account.GetProperty("isActive").GetBoolean(),
                MustChangePassword: account.ValueKind == JsonValueKind.Undefined ? false : account.GetProperty("mustChangePassword").GetBoolean(),
                CreatedAtUtc: account.ValueKind == JsonValueKind.Undefined ? profile.GetProperty("createdAtUtc").GetDateTime() : account.GetProperty("createdAtUtc").GetDateTime(),
                LastLoginAtUtc: account.ValueKind == JsonValueKind.Undefined || !account.TryGetProperty("lastLoginAtUtc", out var lastLogin) || lastLogin.ValueKind == JsonValueKind.Null
                    ? (DateTime?)null
                    : lastLogin.GetDateTime()));
        }

        return Ok(merged.OrderBy(x => x.FullName));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSchoolUserRequest request, CancellationToken cancellationToken)
    {
        var temporaryPassword = GenerateTemporaryPassword();
        var scopeLabel = $"colaborador-escola-{GetCurrentSchoolId()}";
        var onboardingUrl = GetCollaboratorOnboardingUrl();
        var identityClient = CreateAuthorizedClient("identity");
        var createUserResponse = await identityClient.PostAsJsonAsync(
            "/api/v1/users",
            new
            {
                request.Email,
                Password = temporaryPassword,
                request.Role,
                request.Permissions,
                MustChangePassword = true,
                request.IsActive,
                DeliverTemporaryPasswordByEmail = false,
                request.FullName,
                ScopeLabel = scopeLabel,
                OnboardingUrl = onboardingUrl
            },
            cancellationToken);

        if (!createUserResponse.IsSuccessStatusCode)
        {
            return await BuildErrorResult("identity", createUserResponse, cancellationToken);
        }

        var createdUser = await ReadJsonAsync(createUserResponse, cancellationToken);
        var identityUserId = createdUser?.GetProperty("id").GetGuid();
        if (!identityUserId.HasValue)
        {
            return StatusCode(StatusCodes.Status502BadGateway, "O serviço de identidade retornou um payload inválido.");
        }

        var schoolsClient = CreateAuthorizedClient("schools");
        var createProfileResponse = await schoolsClient.PostAsJsonAsync(
            "/api/v1/users",
            new
            {
                identityUserId,
                request.FullName,
                request.Phone,
                request.SalaryAmount,
                request.AvatarUrl,
                request.IsActive
            },
            cancellationToken);

        if (!createProfileResponse.IsSuccessStatusCode)
        {
            return await BuildErrorResult(
                "schools",
                createProfileResponse,
                cancellationToken,
                "A conta foi criada no Identity, mas a criação do perfil da escola falhou. A compensação ainda não é automática.");
        }

        var createdProfile = await ReadJsonAsync(createProfileResponse, cancellationToken);
        if (request.Role == 4)
        {
            var provisionStudentResult = await ProvisionStudentAsync(
                schoolId: GetCurrentSchoolId(),
                identityUserId: identityUserId.Value,
                fullName: request.FullName,
                email: request.Email,
                phone: request.Phone,
                cancellationToken);

            if (provisionStudentResult is not null)
            {
                return provisionStudentResult;
            }
        }

        var financialSyncResult = await SyncAdministrativeCompensationAsync(
            schoolId: GetCurrentSchoolId(),
            identityUserId: identityUserId.Value,
            fullName: request.FullName,
            role: request.Role,
            salaryAmount: request.SalaryAmount,
            isActive: request.IsActive,
            cancellationToken);

        if (financialSyncResult is not null)
        {
            return financialSyncResult;
        }

        var deliveryResponse = await identityClient.PostAsJsonAsync(
            $"/api/v1/users/{identityUserId.Value}/reset-password",
            new
            {
                temporaryPassword,
                mustChangePassword = true,
                deliverByEmail = true,
                request.Email,
                request.FullName,
                ScopeLabel = scopeLabel,
                OnboardingUrl = onboardingUrl
            },
            cancellationToken);

        if (!deliveryResponse.IsSuccessStatusCode)
        {
            return await BuildErrorResult(
                "identity",
                deliveryResponse,
                cancellationToken,
                "A conta foi criada, mas o envio do acesso inicial do colaborador por e-mail falhou.");
        }

        var deliveryPayload = await ReadJsonAsync(deliveryResponse, cancellationToken);

        return Ok(new
        {
            profileId = createdProfile?.GetProperty("id").GetGuid(),
            identityUserId,
            request.FullName,
            request.Email,
            request.Role,
            request.Phone,
            request.AvatarUrl,
            request.IsActive,
            mustChangePassword = true,
            deliveryMode = deliveryPayload?.TryGetProperty("deliveryMode", out var createDeliveryMode) == true ? createDeliveryMode.GetString() : null,
            outboxFilePath = deliveryPayload?.TryGetProperty("outboxFilePath", out var createOutbox) == true ? createOutbox.GetString() : null
        });
    }

    [HttpPut("{identityUserId:guid}")]
    public async Task<IActionResult> Update(
        Guid identityUserId,
        [FromBody] UpdateSchoolUserRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.IsActive)
        {
            var impactResult = await EnsureLinkedUserCanBeDeactivatedAsync(identityUserId, cancellationToken);
            if (impactResult is not null)
            {
                return impactResult;
            }
        }

        var identityClient = CreateAuthorizedClient("identity");
        var identityResponse = await identityClient.PutAsJsonAsync(
            $"/api/v1/users/{identityUserId}",
            new
            {
                request.Role,
                request.Permissions,
                request.MustChangePassword,
                request.IsActive
            },
            cancellationToken);

        if (!identityResponse.IsSuccessStatusCode)
        {
            return await BuildErrorResult("identity", identityResponse, cancellationToken);
        }

        var schoolsClient = CreateAuthorizedClient("schools");
        var profileResponse = await schoolsClient.PutAsJsonAsync(
            $"/api/v1/users/{request.ProfileId}",
            new
            {
                request.FullName,
                request.Phone,
                request.SalaryAmount,
                request.AvatarUrl,
                request.IsActive
            },
            cancellationToken);

        if (!profileResponse.IsSuccessStatusCode)
        {
            return await BuildErrorResult(
                "schools",
                profileResponse,
                cancellationToken,
                "A conta foi atualizada no Identity, mas a atualização do perfil da escola falhou.");
        }

        var syncResult = await SyncLinkedUserStateAsync(identityUserId, request.IsActive, cancellationToken);
        if (syncResult is not null)
        {
            return syncResult;
        }

        var financialSyncResult = await SyncAdministrativeCompensationAsync(
            schoolId: GetCurrentSchoolId(),
            identityUserId: identityUserId,
            fullName: request.FullName,
            role: request.Role,
            salaryAmount: request.SalaryAmount,
            isActive: request.IsActive,
            cancellationToken);

        if (financialSyncResult is not null)
        {
            return financialSyncResult;
        }

        return Ok();
    }

    [HttpGet("invitations")]
    public async Task<IActionResult> GetInvitations(CancellationToken cancellationToken)
    {
        var identityClient = CreateAuthorizedClient("identity");
        var response = await identityClient.GetAsync("/api/v1/invitations", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return await BuildErrorResult("identity", response, cancellationToken);
        }

        var payload = await ReadJsonAsync(response, cancellationToken);
        return Ok(payload);
    }

    [HttpPost("invitations")]
    public async Task<IActionResult> CreateInvitation([FromBody] CreateInvitationRequest request, CancellationToken cancellationToken)
    {
        var identityClient = CreateAuthorizedClient("identity");
        var response = await identityClient.PostAsJsonAsync(
            "/api/v1/invitations",
            new
            {
                request.Email,
                request.FullName,
                request.Role,
                request.Phone,
                request.ExpiresInDays,
                request.SchoolDisplayName,
                request.SchoolSlug
            },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return await BuildErrorResult("identity", response, cancellationToken);
        }

        var payload = await ReadJsonAsync(response, cancellationToken);

        return Ok(new
        {
            id = payload?.GetProperty("id").GetGuid(),
            email = payload?.GetProperty("email").GetString(),
            fullName = payload?.GetProperty("fullName").GetString(),
            phone = payload?.TryGetProperty("phone", out var phone) == true ? phone.GetString() : null,
            role = payload?.GetProperty("role").GetString(),
            expiresAtUtc = payload?.GetProperty("expiresAtUtc").GetDateTime(),
            createdAtUtc = payload?.GetProperty("createdAtUtc").GetDateTime(),
            status = payload?.GetProperty("status").GetString(),
            deliveryMode = payload?.TryGetProperty("deliveryMode", out var deliveryMode) == true ? deliveryMode.GetString() : null,
            inviteLink = payload?.TryGetProperty("temporaryLink", out var temporaryLink) == true ? temporaryLink.GetString() : null,
            outboxFilePath = payload?.TryGetProperty("outboxFilePath", out var outboxFilePath) == true ? outboxFilePath.GetString() : null
        });
    }

    [HttpPost("invitations/{id:guid}/cancel")]
    public async Task<IActionResult> CancelInvitation(Guid id, CancellationToken cancellationToken)
    {
        var identityClient = CreateAuthorizedClient("identity");
        var response = await identityClient.PostAsync($"/api/v1/invitations/{id}/cancel", null, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return await BuildErrorResult("identity", response, cancellationToken);
        }

        return Ok();
    }

    [AllowAnonymous]
    [HttpGet("invitations/preview")]
    public async Task<IActionResult> PreviewInvitation([FromQuery] string token, CancellationToken cancellationToken)
    {
        var identityClient = _httpClientFactory.CreateClient("identity");
        var response = await identityClient.GetAsync(
            $"/api/v1/invitations/preview?token={Uri.EscapeDataString(token ?? string.Empty)}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return await BuildErrorResult("identity", response, cancellationToken);
        }

        var payload = await ReadJsonAsync(response, cancellationToken);
        return Ok(payload);
    }

    [AllowAnonymous]
    [HttpPost("invitations/accept")]
    public async Task<IActionResult> AcceptInvitation([FromBody] AcceptInvitationRequest request, CancellationToken cancellationToken)
    {
        var identityClient = _httpClientFactory.CreateClient("identity");
        var acceptResponse = await identityClient.PostAsJsonAsync(
            "/api/v1/invitations/accept",
            new
            {
                request.Token,
                request.Password
            },
            cancellationToken);

        if (!acceptResponse.IsSuccessStatusCode)
        {
            return await BuildErrorResult("identity", acceptResponse, cancellationToken);
        }

        var acceptedPayload = await ReadJsonAsync(acceptResponse, cancellationToken);
        if (acceptedPayload is null)
        {
            return StatusCode(StatusCodes.Status502BadGateway, "O serviço de identidade retornou um payload inválido.");
        }

        var schoolsClient = CreateInternalClient("schools");
        var schoolProfileResponse = await schoolsClient.PostAsJsonAsync(
            "/api/v1/onboarding/register-invited-user",
            new
            {
                schoolId = acceptedPayload.Value.GetProperty("schoolId").GetGuid(),
                identityUserId = acceptedPayload.Value.GetProperty("userId").GetGuid(),
                fullName = acceptedPayload.Value.GetProperty("fullName").GetString(),
                phone = acceptedPayload.Value.TryGetProperty("phone", out var phone) ? phone.GetString() : null
            },
            cancellationToken);

        if (!schoolProfileResponse.IsSuccessStatusCode)
        {
            return await BuildErrorResult(
                "schools",
                schoolProfileResponse,
                cancellationToken,
                "A conta foi criada no Identity, mas o perfil da escola não foi provisionado automaticamente.");
        }

        if (string.Equals(
                acceptedPayload.Value.GetProperty("role").GetString(),
                "Student",
                StringComparison.Ordinal))
        {
            var provisionStudentResult = await ProvisionStudentAsync(
                schoolId: acceptedPayload.Value.GetProperty("schoolId").GetGuid(),
                identityUserId: acceptedPayload.Value.GetProperty("userId").GetGuid(),
                fullName: acceptedPayload.Value.GetProperty("fullName").GetString() ?? string.Empty,
                email: acceptedPayload.Value.GetProperty("email").GetString() ?? string.Empty,
                phone: acceptedPayload.Value.TryGetProperty("phone", out var acceptedPhone) ? acceptedPhone.GetString() : null,
                cancellationToken);

            if (provisionStudentResult is not null)
            {
                return provisionStudentResult;
            }
        }
        else if (string.Equals(
                     acceptedPayload.Value.GetProperty("role").GetString(),
                     "Instructor",
                     StringComparison.Ordinal))
        {
            var provisionInstructorResult = await ProvisionInstructorAsync(
                schoolId: acceptedPayload.Value.GetProperty("schoolId").GetGuid(),
                identityUserId: acceptedPayload.Value.GetProperty("userId").GetGuid(),
                fullName: acceptedPayload.Value.GetProperty("fullName").GetString() ?? string.Empty,
                email: acceptedPayload.Value.GetProperty("email").GetString(),
                phone: acceptedPayload.Value.TryGetProperty("phone", out var instructorPhone) ? instructorPhone.GetString() : null,
                cancellationToken);

            if (provisionInstructorResult is not null)
            {
                return provisionInstructorResult;
            }
        }

        var loginResponse = await identityClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new
            {
                email = acceptedPayload.Value.GetProperty("email").GetString(),
                request.Password
            },
            cancellationToken);

        if (!loginResponse.IsSuccessStatusCode)
        {
            return await BuildErrorResult("identity", loginResponse, cancellationToken);
        }

        var sessionPayload = await ReadJsonAsync(loginResponse, cancellationToken);

        return Ok(new
        {
            invitationAccepted = true,
            session = sessionPayload
        });
    }

    [HttpPost("{identityUserId:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(
        Guid identityUserId,
        [FromBody] ResetSchoolUserPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var temporaryPassword = GenerateTemporaryPassword();
        var identityClient = CreateAuthorizedClient("identity");
        var response = await identityClient.PostAsJsonAsync(
            $"/api/v1/users/{identityUserId}/reset-password",
            new
            {
                temporaryPassword,
                mustChangePassword = true,
                deliverByEmail = request.DeliverByEmail,
                request.Email,
                request.FullName,
                ScopeLabel = $"colaborador-escola-{GetCurrentSchoolId()}",
                OnboardingUrl = GetCollaboratorOnboardingUrl()
            },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return await BuildErrorResult("identity", response, cancellationToken);
        }

        var payload = await ReadJsonAsync(response, cancellationToken);
        return Ok(payload);
    }

    private HttpClient CreateAuthorizedClient(string name)
    {
        var client = _httpClientFactory.CreateClient(name);
        var authorization = Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization) &&
            AuthenticationHeaderValue.TryParse(authorization, out var header))
        {
            client.DefaultRequestHeaders.Authorization = header;
        }

        return client;
    }

    private HttpClient CreateInternalClient(string name)
    {
        var client = _httpClientFactory.CreateClient(name);
        var sharedKey = _configuration["InternalServiceAuth:SharedKey"];
        if (!string.IsNullOrWhiteSpace(sharedKey))
        {
            client.DefaultRequestHeaders.Remove(InternalServiceKeyHeader);
            client.DefaultRequestHeaders.Add(InternalServiceKeyHeader, sharedKey);
        }

        return client;
    }

    private static async Task<IActionResult> BuildErrorResult(
        string service,
        HttpResponseMessage response,
        CancellationToken cancellationToken,
        string? detail = null)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return new ObjectResult(new
        {
            service,
            statusCode = (int)response.StatusCode,
            detail,
            body
        })
        {
            StatusCode = response.StatusCode == HttpStatusCode.Conflict
                ? StatusCodes.Status409Conflict
                : (int)response.StatusCode
        };
    }

    private Guid GetCurrentSchoolId()
    {
        var schoolIdRaw = User.FindFirstValue("school_id");
        if (Guid.TryParse(schoolIdRaw, out var schoolId) && schoolId != Guid.Empty)
        {
            return schoolId;
        }

        throw new InvalidOperationException("Não foi possível identificar a escola atual no token.");
    }

    private string GetCollaboratorOnboardingUrl()
    {
        var url = _configuration["OwnerCredentialDelivery:PublicLoginUrl"];
        return string.IsNullOrWhiteSpace(url) ? "http://localhost:5174/login" : url;
    }

    private async Task<IActionResult?> ProvisionStudentAsync(
        Guid schoolId,
        Guid identityUserId,
        string fullName,
        string email,
        string? phone,
        CancellationToken cancellationToken)
    {
        if (schoolId == Guid.Empty)
        {
            return Unauthorized("Não foi possível identificar a escola atual para provisionar o aluno.");
        }

        var academicsClient = CreateInternalClient("academics");
        var response = await academicsClient.PostAsJsonAsync(
            "/api/v1/onboarding/register-invited-student",
            new
            {
                schoolId,
                identityUserId,
                fullName,
                email,
                phone
            },
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return null;
        }

        return await BuildErrorResult(
            "academics",
            response,
            cancellationToken,
            "A conta foi criada, mas o cadastro acadêmico do aluno não foi provisionado automaticamente.");
    }

    private async Task<IActionResult?> ProvisionInstructorAsync(
        Guid schoolId,
        Guid identityUserId,
        string fullName,
        string? email,
        string? phone,
        CancellationToken cancellationToken)
    {
        if (schoolId == Guid.Empty)
        {
            return Unauthorized("Não foi possível identificar a escola atual para provisionar o instrutor.");
        }

        var academicsClient = CreateInternalClient("academics");
        var response = await academicsClient.PostAsJsonAsync(
            "/api/v1/onboarding/register-invited-instructor",
            new
            {
                schoolId,
                identityUserId,
                fullName,
                email,
                phone
            },
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return null;
        }

        return await BuildErrorResult(
            "academics",
            response,
            cancellationToken,
            "A conta foi criada, mas o cadastro acadêmico do instrutor não foi provisionado automaticamente.");
    }

    private async Task<IActionResult?> SyncLinkedUserStateAsync(
        Guid identityUserId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var academicsClient = CreateInternalClient("academics");
        var response = await academicsClient.PostAsJsonAsync(
            "/api/v1/onboarding/sync-linked-user-state",
            new
            {
                schoolId = GetCurrentSchoolId(),
                identityUserId,
                isActive
            },
            cancellationToken);

        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await BuildErrorResult(
            "academics",
            response,
            cancellationToken,
            "A conta foi atualizada, mas o sincronismo acadêmico do colaborador falhou.");
    }

    private async Task<IActionResult?> SyncAdministrativeCompensationAsync(
        Guid schoolId,
        Guid identityUserId,
        string fullName,
        int role,
        decimal? salaryAmount,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var isAdministrative = role == 5;
        var financeClient = CreateInternalClient("finance");
        var response = await financeClient.PostAsJsonAsync(
            "/api/v1/internal/finance/expenses/automation",
            new
            {
                SchoolId = schoolId,
                SourceType = "SchoolUserSalary",
                SourceId = identityUserId,
                Category = 3,
                Amount = isAdministrative ? decimal.Round(Math.Max(salaryAmount ?? 0m, 0m), 2) : 0m,
                OccurredAtUtc = DateTime.UtcNow,
                Description = $"Salário administrativo - {fullName.Trim()}",
                Vendor = (string?)null,
                IsActive = isAdministrative && isActive && salaryAmount.GetValueOrDefault() > 0m
            },
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return null;
        }

        return await BuildErrorResult(
            "finance",
            response,
            cancellationToken,
            "O colaborador foi salvo, mas a sincronização da remuneração administrativa com o financeiro falhou.");
    }

    private async Task<IActionResult?> EnsureLinkedUserCanBeDeactivatedAsync(Guid identityUserId, CancellationToken cancellationToken)
    {
        var academicsClient = CreateInternalClient("academics");
        var response = await academicsClient.PostAsJsonAsync(
            "/api/v1/onboarding/deactivation-impact",
            new
            {
                schoolId = GetCurrentSchoolId(),
                identityUserId
            },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return await BuildErrorResult(
                "academics",
                response,
                cancellationToken,
                "Não foi possível validar o impacto acadêmico da desativação.");
        }

        var payload = await ReadJsonAsync(response, cancellationToken);
        var canDeactivate = payload?.TryGetProperty("canDeactivate", out var canDeactivateProperty) == true &&
            canDeactivateProperty.GetBoolean();

        if (canDeactivate)
        {
            return null;
        }

        var messages = payload?.TryGetProperty("messages", out var messagesProperty) == true &&
            messagesProperty.ValueKind == JsonValueKind.Array
            ? messagesProperty.EnumerateArray()
                .Select(x => x.GetString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray()
            : Array.Empty<string>();

        return Conflict(new
        {
            detail = "A desativação desta conta ainda impacta a operação da escola.",
            messages
        });
    }

    private async Task<JsonElement?> GetJsonAsync(string clientName, string path, CancellationToken cancellationToken)
    {
        var client = CreateAuthorizedClient(clientName);
        var response = await client.GetAsync(path, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await ReadJsonAsync(response, cancellationToken);
    }

    private static async Task<JsonElement?> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.Clone();
    }

    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%&*?";

        var all = $"{upper}{lower}{digits}{symbols}";
        var chars = new List<char>
        {
            upper[RandomNumberGenerator.GetInt32(upper.Length)],
            lower[RandomNumberGenerator.GetInt32(lower.Length)],
            digits[RandomNumberGenerator.GetInt32(digits.Length)],
            symbols[RandomNumberGenerator.GetInt32(symbols.Length)]
        };

        while (chars.Count < 12)
        {
            chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);
        }

        for (var index = chars.Count - 1; index > 0; index--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (chars[index], chars[swapIndex]) = (chars[swapIndex], chars[index]);
        }

        return new string(chars.ToArray());
    }

    public sealed record CreateSchoolUserRequest(
        string FullName,
        string Email,
        int Role,
        IReadOnlyCollection<string>? Permissions,
        string? Phone,
        decimal? SalaryAmount,
        string? AvatarUrl,
        bool IsActive,
        bool MustChangePassword);

    public sealed record UpdateSchoolUserRequest(
        Guid ProfileId,
        string FullName,
        int Role,
        IReadOnlyCollection<string>? Permissions,
        string? Phone,
        decimal? SalaryAmount,
        string? AvatarUrl,
        bool IsActive,
        bool MustChangePassword);

    public sealed record CreateInvitationRequest(
        string Email,
        string FullName,
        int Role,
        string? Phone,
        int ExpiresInDays,
        string? SchoolDisplayName = null,
        string? SchoolSlug = null);

    public sealed record AcceptInvitationRequest(
        string Token,
        string Password);

    public sealed record ResetSchoolUserPasswordRequest(
        bool DeliverByEmail = true,
        string? Email = null,
        string? FullName = null);

    public sealed record SchoolUserListItem(
        Guid ProfileId,
        Guid IdentityUserId,
        string FullName,
        string? Phone,
        decimal? SalaryAmount,
        string? AvatarUrl,
        bool ProfileIsActive,
        string? Email,
        string? Role,
        IReadOnlyCollection<string> Permissions,
        bool IsActive,
        bool MustChangePassword,
        DateTime CreatedAtUtc,
        DateTime? LastLoginAtUtc);
}
