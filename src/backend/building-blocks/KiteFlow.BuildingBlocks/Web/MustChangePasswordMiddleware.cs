using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace KiteFlow.BuildingBlocks.Web;

public sealed class MustChangePasswordMiddleware
{
    private static readonly string[] AllowedPaths =
    [
        "/api/v1/auth/login",
        "/api/v1/auth/refresh",
        "/api/v1/auth/logout",
        "/api/v1/auth/me",
        "/api/v1/auth/change-password",
        "/identity/api/v1/auth/login",
        "/identity/api/v1/auth/refresh",
        "/identity/api/v1/auth/logout",
        "/identity/api/v1/auth/me",
        "/identity/api/v1/auth/change-password"
    ];

    private readonly RequestDelegate _next;

    public MustChangePasswordMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        if (IsAllowedPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var mustChangePassword = string.Equals(
            context.User.FindFirstValue("must_change_password"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (!mustChangePassword)
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            message = "É preciso trocar a senha temporária antes de continuar.",
            code = "must_change_password"
        });
    }

    private static bool IsAllowedPath(PathString path)
    {
        if (path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/favicon", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return AllowedPaths.Any(item => path.Equals(new PathString(item), StringComparison.OrdinalIgnoreCase));
    }
}
