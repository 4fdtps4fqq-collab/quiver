using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using KiteFlow.Gateway.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KiteFlow.Gateway.Controllers;

[ApiController]
[Authorize(Policy = "SystemAdminOnly")]
[Route("api/v1/system/schools")]
public sealed class SystemSchoolsController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOwnerCredentialDeliveryService _credentialDeliveryService;

    public SystemSchoolsController(
        IHttpClientFactory httpClientFactory,
        IOwnerCredentialDeliveryService credentialDeliveryService)
    {
        _httpClientFactory = httpClientFactory;
        _credentialDeliveryService = credentialDeliveryService;
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

        var payload = await ReadJsonAsync(response, cancellationToken);
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

        var delivery = await _credentialDeliveryService.SendTemporaryPasswordAsync(
            new OwnerCredentialDeliveryMessage(
                request.OwnerFullName,
                request.OwnerEmail,
                request.DisplayName,
                request.Slug ?? request.DisplayName,
                temporaryPassword,
                "http://localhost:5174/login"),
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
}
