using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KiteFlow.Gateway.Controllers;

[ApiController]
[Authorize(Policy = "StudentsAccess")]
[Route("api/v1/students")]
public sealed class StudentPortalAccessController : ControllerBase
{
    private const string InternalServiceKeyHeader = "X-KiteFlow-Internal-Key";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public StudentPortalAccessController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [HttpPost("{studentId:guid}/portal-access")]
    public async Task<IActionResult> IssuePortalAccess(Guid studentId, CancellationToken cancellationToken)
    {
        var student = await GetStudentAsync(studentId, cancellationToken);
        if (student is null)
        {
            return NotFound("Aluno não encontrado.");
        }

        if (!student.IsActive)
        {
            return BadRequest("Ative o aluno antes de gerar o acesso ao portal.");
        }

        if (string.IsNullOrWhiteSpace(student.Email))
        {
            return BadRequest("Cadastre um e-mail válido para enviar o acesso ao portal do aluno.");
        }

        var identityUsers = await GetIdentityUsersAsync(cancellationToken);
        if (identityUsers is null)
        {
            return StatusCode(StatusCodes.Status502BadGateway, "O serviço de identidade não respondeu.");
        }

        var linkedAccount = ResolveLinkedAccount(student, identityUsers.Value);
        var temporaryPassword = GenerateTemporaryPassword();
        var scopeLabel = $"portal-aluno-{GetCurrentSchoolId()}";
        var identityClient = CreateAuthorizedClient("identity");

        Guid identityUserId;
        string? deliveryMode;
        string? outboxFilePath;
        var createdNewAccount = false;

        if (linkedAccount is null)
        {
            var createResponse = await identityClient.PostAsJsonAsync(
                "/api/v1/users",
                new
                {
                    Email = student.Email,
                    Password = temporaryPassword,
                    Role = 4,
                    Permissions = (string[]?)null,
                    MustChangePassword = true,
                    IsActive = student.IsActive,
                    DeliverTemporaryPasswordByEmail = true,
                    FullName = student.FullName,
                    ScopeLabel = scopeLabel
                },
                cancellationToken);

            if (!createResponse.IsSuccessStatusCode)
            {
                return await BuildErrorResult("identity", createResponse, cancellationToken);
            }

            var createdPayload = await ReadJsonAsync(createResponse, cancellationToken);
            if (createdPayload is null || !createdPayload.Value.TryGetProperty("id", out var idProperty))
            {
                return StatusCode(StatusCodes.Status502BadGateway, "O serviço de identidade retornou um payload inválido.");
            }

            identityUserId = idProperty.GetGuid();
            deliveryMode = createdPayload.Value.TryGetProperty("deliveryMode", out var createDeliveryMode) ? createDeliveryMode.GetString() : null;
            outboxFilePath = createdPayload.Value.TryGetProperty("outboxFilePath", out var createOutbox) ? createOutbox.GetString() : null;
            createdNewAccount = true;
        }
        else
        {
            if (!string.Equals(linkedAccount.Role, "Student", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict("Já existe uma conta ativa com este e-mail vinculada a outro tipo de acesso. Use um e-mail exclusivo para o portal do aluno.");
            }

            identityUserId = linkedAccount.Id;

            var resetResponse = await identityClient.PostAsJsonAsync(
                $"/api/v1/users/{identityUserId}/reset-password",
                new
                {
                    TemporaryPassword = temporaryPassword,
                    MustChangePassword = true,
                    DeliverByEmail = true,
                    Email = student.Email,
                    FullName = student.FullName,
                    ScopeLabel = scopeLabel
                },
                cancellationToken);

            if (!resetResponse.IsSuccessStatusCode)
            {
                return await BuildErrorResult("identity", resetResponse, cancellationToken);
            }

            var resetPayload = await ReadJsonAsync(resetResponse, cancellationToken);
            deliveryMode = resetPayload?.TryGetProperty("deliveryMode", out var resetDeliveryMode) == true ? resetDeliveryMode.GetString() : null;
            outboxFilePath = resetPayload?.TryGetProperty("outboxFilePath", out var resetOutbox) == true ? resetOutbox.GetString() : null;
        }

        var provisionResult = await ProvisionStudentAsync(
            GetCurrentSchoolId(),
            identityUserId,
            student.FullName,
            student.Email,
            student.Phone,
            cancellationToken);

        if (provisionResult is not null)
        {
            return provisionResult;
        }

        return Ok(new
        {
            studentId = student.Id,
            identityUserId,
            createdNewAccount,
            mustChangePassword = true,
            deliveryMode,
            outboxFilePath,
            issuedAtUtc = DateTime.UtcNow
        });
    }

    private async Task<StudentPortalStudent?> GetStudentAsync(Guid studentId, CancellationToken cancellationToken)
    {
        var payload = await GetJsonAsync("academics", "/api/v1/students?activeOnly=false", cancellationToken);
        if (payload is null || payload.Value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in payload.Value.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idProperty) || idProperty.GetGuid() != studentId)
            {
                continue;
            }

            return new StudentPortalStudent(
                Id: idProperty.GetGuid(),
                FullName: item.GetProperty("fullName").GetString() ?? string.Empty,
                Email: item.TryGetProperty("email", out var emailProperty) && emailProperty.ValueKind != JsonValueKind.Null ? emailProperty.GetString() : null,
                Phone: item.TryGetProperty("phone", out var phoneProperty) && phoneProperty.ValueKind != JsonValueKind.Null ? phoneProperty.GetString() : null,
                IdentityUserId: item.TryGetProperty("identityUserId", out var identityUserIdProperty) && identityUserIdProperty.ValueKind != JsonValueKind.Null
                    ? identityUserIdProperty.GetGuid()
                    : null,
                IsActive: item.GetProperty("isActive").GetBoolean());
        }

        return null;
    }

    private async Task<JsonElement?> GetIdentityUsersAsync(CancellationToken cancellationToken)
        => await GetJsonAsync("identity", "/api/v1/users?activeOnly=false", cancellationToken);

    private static LinkedIdentityAccount? ResolveLinkedAccount(StudentPortalStudent student, JsonElement accounts)
    {
        LinkedIdentityAccount? emailMatch = null;

        foreach (var item in accounts.EnumerateArray())
        {
            var accountId = item.GetProperty("id").GetGuid();
            var email = item.TryGetProperty("email", out var emailProperty) ? emailProperty.GetString() : null;
            var role = item.TryGetProperty("role", out var roleProperty) ? roleProperty.GetString() : null;

            if (student.IdentityUserId.HasValue && accountId == student.IdentityUserId.Value)
            {
                return new LinkedIdentityAccount(accountId, email, role);
            }

            if (!string.IsNullOrWhiteSpace(student.Email) &&
                !string.IsNullOrWhiteSpace(email) &&
                string.Equals(student.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                emailMatch = new LinkedIdentityAccount(accountId, email, role);
            }
        }

        return emailMatch;
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

    private async Task<IActionResult?> ProvisionStudentAsync(
        Guid schoolId,
        Guid identityUserId,
        string fullName,
        string email,
        string? phone,
        CancellationToken cancellationToken)
    {
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
            "A conta do portal foi criada, mas o vínculo acadêmico do aluno não foi sincronizado.");
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

    private Guid GetCurrentSchoolId()
    {
        var schoolIdRaw = User.FindFirstValue("school_id");
        if (Guid.TryParse(schoolIdRaw, out var schoolId) && schoolId != Guid.Empty)
        {
            return schoolId;
        }

        throw new InvalidOperationException("Não foi possível identificar a escola atual no token.");
    }

    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%&*?";
        var all = upper + lower + digits + symbols;

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

    private static async Task<JsonElement?> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.Clone();
    }

    private sealed record StudentPortalStudent(
        Guid Id,
        string FullName,
        string? Email,
        string? Phone,
        Guid? IdentityUserId,
        bool IsActive);

    private sealed record LinkedIdentityAccount(
        Guid Id,
        string? Email,
        string? Role);
}
