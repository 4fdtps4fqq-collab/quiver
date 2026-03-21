using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using KiteFlow.Gateway.Configuration;
using KiteFlow.Gateway.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KiteFlow.Gateway.Controllers;

[ApiController]
[Authorize(Policy = "SystemAdminOnly")]
[Route("api/v1/system/schools")]
public sealed class SystemSchoolsController : ControllerBase
{
    private static readonly JsonSerializerOptions TypedJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOwnerCredentialDeliveryService _credentialDeliveryService;
    private readonly OwnerCredentialDeliveryOptions _credentialDeliveryOptions;

    public SystemSchoolsController(
        IHttpClientFactory httpClientFactory,
        IOwnerCredentialDeliveryService credentialDeliveryService,
        IOptions<OwnerCredentialDeliveryOptions> credentialDeliveryOptions)
    {
        _httpClientFactory = httpClientFactory;
        _credentialDeliveryService = credentialDeliveryService;
        _credentialDeliveryOptions = credentialDeliveryOptions.Value;
    }

    [HttpGet]
    public async Task<IActionResult> ListSchools(CancellationToken cancellationToken)
    {
        var schoolsClient = CreateAuthorizedClient("schools");
        var response = await schoolsClient.GetAsync("/api/v1/system/schools", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return await BuildErrorResult("schools", response, cancellationToken);
        }

        var schools = await ReadTypedJsonAsync<List<SystemSchoolSummaryResponse>>(response, cancellationToken)
            ?? [];

        if (schools.Count == 0)
        {
            return Ok(schools);
        }

        var ownerEmailsBySchool = await GetOwnerEmailsBySchoolAsync(schools.Select(x => x.Id), cancellationToken);

        var payload = schools.Select(school => new
        {
            school.Id,
            school.LegalName,
            school.DisplayName,
            school.Slug,
            school.LogoDataUrl,
            school.BaseBeachName,
            school.BaseLatitude,
            school.BaseLongitude,
            school.Status,
            school.Timezone,
            school.CurrencyCode,
            school.CreatedAtUtc,
            school.UsersCount,
            school.OwnerName,
            ownerEmail = ownerEmailsBySchool.TryGetValue(school.Id, out var ownerEmail) ? ownerEmail : null
        });

        return Ok(payload);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetSchool(Guid id, CancellationToken cancellationToken)
    {
        var schoolsClient = CreateAuthorizedClient("schools");
        var response = await schoolsClient.GetAsync($"/api/v1/system/schools/{id}", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return await BuildErrorResult("schools", response, cancellationToken);
        }

        var payload = await ReadJsonAsync(response, cancellationToken);
        return Ok(payload);
    }

    [HttpPost]
    public async Task<IActionResult> CreateSchool([FromBody] CreateSystemSchoolRequest request, CancellationToken cancellationToken)
    {
        var schoolId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var temporaryPassword = GenerateTemporaryPassword();

        var schoolsClient = CreateAuthorizedClient("schools");
        var schoolResponse = await schoolsClient.PostAsJsonAsync(
            "/api/v1/onboarding/register-school",
            new
            {
                schoolId,
                ownerIdentityUserId = ownerUserId,
                request.LegalName,
                request.DisplayName,
                request.Cnpj,
                request.BaseBeachName,
                request.BaseLatitude,
                request.BaseLongitude,
                request.PostalCode,
                request.Street,
                request.StreetNumber,
                request.AddressComplement,
                request.Neighborhood,
                request.City,
                request.State,
                request.OwnerFullName,
                request.OwnerCpf,
                request.OwnerPhone,
                request.OwnerPostalCode,
                request.OwnerStreet,
                request.OwnerStreetNumber,
                request.OwnerAddressComplement,
                request.OwnerNeighborhood,
                request.OwnerCity,
                request.OwnerState,
                request.Slug,
                request.Timezone,
                request.CurrencyCode,
                request.LogoDataUrl,
                request.ThemePrimary,
                request.ThemeAccent,
                request.BookingLeadTimeMinutes,
                request.CancellationWindowHours
            },
            cancellationToken);

        if (!schoolResponse.IsSuccessStatusCode)
        {
            return await BuildErrorResult("schools", schoolResponse, cancellationToken);
        }

        var identityClient = CreateAuthorizedClient("identity");
        var bootstrapResponse = await identityClient.PostAsJsonAsync(
            "/api/v1/auth/bootstrap-user",
            new
            {
                userId = ownerUserId,
                schoolId,
                Email = request.OwnerEmail,
                Password = temporaryPassword,
                role = 2,
                MustChangePassword = true
            },
            cancellationToken);

        if (!bootstrapResponse.IsSuccessStatusCode)
        {
            return await BuildErrorResult(
                "identity",
                bootstrapResponse,
                cancellationToken,
                "A escola foi criada, mas a conta do proprietário falhou no Identity. A compensação ainda não é automática.");
        }

        var publicLoginUrl = string.IsNullOrWhiteSpace(_credentialDeliveryOptions.PublicLoginUrl)
            ? $"{Request.Scheme}://{Request.Host}/login"
            : _credentialDeliveryOptions.PublicLoginUrl;

        var delivery = await _credentialDeliveryService.SendTemporaryPasswordAsync(
            new OwnerCredentialDeliveryMessage(
                request.OwnerFullName,
                request.OwnerEmail,
                request.DisplayName,
                request.Slug ?? request.DisplayName,
                temporaryPassword,
                publicLoginUrl),
            cancellationToken);

        return Ok(new
        {
            schoolId,
            ownerUserId,
            createdAtUtc = DateTime.UtcNow,
            temporaryPasswordSent = delivery.Delivered,
            deliveryMode = delivery.Mode,
            outboxFilePath = delivery.OutboxFilePath
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateSchool(Guid id, [FromBody] UpdateSystemSchoolRequest request, CancellationToken cancellationToken)
    {
        var schoolsClient = CreateAuthorizedClient("schools");
        var response = await schoolsClient.PutAsJsonAsync(
            $"/api/v1/system/schools/{id}",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return await BuildErrorResult("schools", response, cancellationToken);
        }

        var payload = await ReadJsonAsync(response, cancellationToken);
        return Ok(payload);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteSchool(Guid id, CancellationToken cancellationToken)
    {
        var downstreamDeletes = new[]
        {
            (Client: "finance", Path: $"/api/v1/system/tenants/{id}"),
            (Client: "equipment", Path: $"/api/v1/system/tenants/{id}"),
            (Client: "academics", Path: $"/api/v1/system/tenants/{id}"),
            (Client: "identity", Path: $"/api/v1/system/tenants/{id}"),
            (Client: "schools", Path: $"/api/v1/system/schools/{id}")
        };

        foreach (var item in downstreamDeletes)
        {
            var client = CreateAuthorizedClient(item.Client);
            var response = await client.DeleteAsync(item.Path, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return await BuildErrorResult(
                    item.Client,
                    response,
                    cancellationToken,
                    "A exclusão da escola foi interrompida em um dos microsserviços. Revise o estado do tenant antes de tentar novamente.");
            }
        }

        return Ok(new
        {
            schoolId = id,
            deletedAtUtc = DateTime.UtcNow
        });
    }

    private static string GenerateTemporaryPassword()
    {
        const string uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lowercase = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%*?";
        var all = uppercase + lowercase + digits + symbols;

        var chars = new List<char>
        {
            uppercase[RandomNumberGenerator.GetInt32(uppercase.Length)],
            lowercase[RandomNumberGenerator.GetInt32(lowercase.Length)],
            digits[RandomNumberGenerator.GetInt32(digits.Length)],
            symbols[RandomNumberGenerator.GetInt32(symbols.Length)]
        };

        while (chars.Count < 12)
        {
            chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);
        }

        return new string(chars.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToArray());
    }

    private HttpClient CreateAuthorizedClient(string clientName)
    {
        var client = _httpClientFactory.CreateClient(clientName);
        var authorization = Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization) && AuthenticationHeaderValue.TryParse(authorization, out var header))
        {
            client.DefaultRequestHeaders.Authorization = header;
        }

        return client;
    }

    private async Task<Dictionary<Guid, string>> GetOwnerEmailsBySchoolAsync(
        IEnumerable<Guid> schoolIds,
        CancellationToken cancellationToken)
    {
        var normalizedIds = schoolIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();

        if (normalizedIds.Length == 0)
        {
            return [];
        }

        var queryString = string.Join("&", normalizedIds.Select(id => $"schoolIds={id}"));
        var identityClient = CreateAuthorizedClient("identity");
        var response = await identityClient.GetAsync($"/api/v1/system/tenants/owners?{queryString}", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var owners = await ReadTypedJsonAsync<List<SchoolOwnerEmailResponse>>(response, cancellationToken)
            ?? [];

        return owners
            .Where(x => x.SchoolId != Guid.Empty && !string.IsNullOrWhiteSpace(x.Email))
            .GroupBy(x => x.SchoolId)
            .ToDictionary(group => group.Key, group => group.First().Email);
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

    private static async Task<T?> ReadTypedJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, TypedJsonOptions, cancellationToken);
    }

    public sealed record CreateSystemSchoolRequest(
        string LegalName,
        string DisplayName,
        string? Cnpj,
        string BaseBeachName,
        double? BaseLatitude,
        double? BaseLongitude,
        string PostalCode,
        string Street,
        string StreetNumber,
        string? AddressComplement,
        string Neighborhood,
        string City,
        string State,
        string OwnerFullName,
        string OwnerEmail,
        string OwnerCpf,
        string? OwnerPhone,
        string OwnerPostalCode,
        string OwnerStreet,
        string OwnerStreetNumber,
        string? OwnerAddressComplement,
        string OwnerNeighborhood,
        string OwnerCity,
        string OwnerState,
        string? Slug,
        string? Timezone,
        string? CurrencyCode,
        string? LogoDataUrl,
        string? ThemePrimary,
        string? ThemeAccent,
        int BookingLeadTimeMinutes = 60,
        int CancellationWindowHours = 24);

    public sealed record UpdateSystemSchoolRequest(
        string LegalName,
        string DisplayName,
        string? Cnpj,
        string BaseBeachName,
        double? BaseLatitude,
        double? BaseLongitude,
        string? LogoDataUrl,
        string PostalCode,
        string Street,
        string StreetNumber,
        string? AddressComplement,
        string Neighborhood,
        string City,
        string State,
        string OwnerFullName,
        string OwnerCpf,
        string? OwnerPhone,
        string OwnerPostalCode,
        string OwnerStreet,
        string OwnerStreetNumber,
        string? OwnerAddressComplement,
        string OwnerNeighborhood,
        string OwnerCity,
        string OwnerState,
        bool OwnerIsActive,
        string Status,
        string? Timezone,
        string? CurrencyCode);

    private sealed record SystemSchoolSummaryResponse(
        Guid Id,
        string LegalName,
        string DisplayName,
        string Slug,
        string? LogoDataUrl,
        string? BaseBeachName,
        double? BaseLatitude,
        double? BaseLongitude,
        string Status,
        string Timezone,
        string CurrencyCode,
        DateTime CreatedAtUtc,
        int UsersCount,
        string? OwnerName);

    private sealed record SchoolOwnerEmailResponse(
        Guid SchoolId,
        Guid UserId,
        string Email);
}
