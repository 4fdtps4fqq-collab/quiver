using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;

namespace KiteFlow.Gateway.Middleware;

public sealed class SchoolAvailabilityMiddleware
{
    private const string InternalServiceKeyHeader = "X-KiteFlow-Internal-Key";
    private static readonly string[] AllowedPrefixes =
    [
        "/swagger",
        "/favicon",
        "/identity/api/v1/auth/login",
        "/identity/api/v1/auth/refresh",
        "/identity/api/v1/auth/logout",
        "/identity/api/v1/auth/me",
        "/identity/api/v1/auth/change-password"
    ];

    private readonly RequestDelegate _next;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public SchoolAvailabilityMiddleware(
        RequestDelegate next,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _next = next;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true ||
            IsAllowedPath(context.Request.Path) ||
            context.User.IsInRole("SystemAdmin"))
        {
            await _next(context);
            return;
        }

        var schoolIdValue = context.User.FindFirstValue("school_id");
        if (!Guid.TryParse(schoolIdValue, out var schoolId))
        {
            await _next(context);
            return;
        }

        var client = _httpClientFactory.CreateClient("schools");
        var sharedKey = _configuration["InternalServiceAuth:SharedKey"];
        if (client.BaseAddress is null || string.IsNullOrWhiteSpace(sharedKey))
        {
            await WriteErrorAsync(
                context,
                StatusCodes.Status503ServiceUnavailable,
                "Não foi possível validar a disponibilidade da escola agora.");
            return;
        }

        client.DefaultRequestHeaders.Remove(InternalServiceKeyHeader);
        client.DefaultRequestHeaders.Add(InternalServiceKeyHeader, sharedKey);

        var response = await client.GetAsync($"/api/v1/internal/schools/{schoolId}/access", context.RequestAborted);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            await WriteErrorAsync(
                context,
                StatusCodes.Status403Forbidden,
                "A escola vinculada a esta conta não está mais disponível.");
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            await WriteErrorAsync(
                context,
                StatusCodes.Status503ServiceUnavailable,
                "Não foi possível validar a disponibilidade da escola agora.");
            return;
        }

        var school = await response.Content.ReadFromJsonAsync<SchoolAccessResponse>(cancellationToken: context.RequestAborted);
        if (school is null || school.IsAccessAllowed)
        {
            await _next(context);
            return;
        }

        var message = school.Status.Equals("Suspended", StringComparison.OrdinalIgnoreCase)
            ? "A escola está inativa no momento. Entre em contato com a administração da plataforma."
            : "A escola ainda não está liberada para operar.";

        await WriteErrorAsync(context, StatusCodes.Status403Forbidden, message);
    }

    private static bool IsAllowedPath(PathString path)
        => AllowedPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));

    private static Task WriteErrorAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(new
        {
            message,
            code = "school_unavailable"
        });
    }

    private sealed record SchoolAccessResponse(
        Guid Id,
        string DisplayName,
        string Status,
        bool IsAccessAllowed);
}
