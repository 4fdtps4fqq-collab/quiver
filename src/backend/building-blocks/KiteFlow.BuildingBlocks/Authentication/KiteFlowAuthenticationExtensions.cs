using System.Text;
using KiteFlow.BuildingBlocks.MultiTenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace KiteFlow.BuildingBlocks.Authentication;

public static class KiteFlowAuthenticationExtensions
{
    public static IServiceCollection AddKiteFlowPlatformAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(static o =>
                !string.IsNullOrWhiteSpace(o.Issuer) &&
                !string.IsNullOrWhiteSpace(o.Audience) &&
                !string.IsNullOrWhiteSpace(o.Key),
                "A configuração de JWT está incompleta.")
            .ValidateOnStart();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentTenant, CurrentTenant>();
        services.AddScoped<ICurrentUser, CurrentUser>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptionsAccessor) =>
            {
                var jwt = jwtOptionsAccessor.Value;

                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ClockSkew = TimeSpan.FromMinutes(2)
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy("SystemAdminOnly", policy => policy.RequireRole("SystemAdmin"))
            .AddPolicy("SchoolAdmins", policy => policy.RequireRole("SystemAdmin", "Owner"))
            .AddPolicy("SchoolStaff", policy => policy.RequireRole("SystemAdmin", "Owner", "Admin", "Instructor"))
            .AddPolicy("SchoolMembers", policy => policy.RequireRole("SystemAdmin", "Owner", "Admin", "Instructor", "Student"))
            .AddPolicy("StudentOnly", policy => policy.RequireRole("Student"))
            .AddPolicy("DashboardAccess", policy => policy.RequireAssertion(ctx => HasModuleAccess(ctx.User, PlatformPermissions.DashboardView)))
            .AddPolicy("StudentsAccess", policy => policy.RequireAssertion(ctx => HasModuleAccess(ctx.User, PlatformPermissions.StudentsManage)))
            .AddPolicy("StudentsReadAccess", policy => policy.RequireAssertion(ctx =>
                HasModuleAccess(ctx.User, PlatformPermissions.StudentsManage) ||
                HasModuleAccess(ctx.User, PlatformPermissions.LessonsManage)))
            .AddPolicy("InstructorsAccess", policy => policy.RequireAssertion(ctx => HasModuleAccess(ctx.User, PlatformPermissions.InstructorsManage)))
            .AddPolicy("InstructorsReadAccess", policy => policy.RequireAssertion(ctx =>
                HasModuleAccess(ctx.User, PlatformPermissions.InstructorsManage) ||
                HasModuleAccess(ctx.User, PlatformPermissions.LessonsManage)))
            .AddPolicy("CoursesAccess", policy => policy.RequireAssertion(ctx => HasModuleAccess(ctx.User, PlatformPermissions.CoursesManage)))
            .AddPolicy("EnrollmentsAccess", policy => policy.RequireAssertion(ctx => HasModuleAccess(ctx.User, PlatformPermissions.EnrollmentsManage)))
            .AddPolicy("EnrollmentsReadAccess", policy => policy.RequireAssertion(ctx =>
                HasModuleAccess(ctx.User, PlatformPermissions.EnrollmentsManage) ||
                HasModuleAccess(ctx.User, PlatformPermissions.LessonsManage)))
            .AddPolicy("LessonsAccess", policy => policy.RequireAssertion(ctx => HasModuleAccess(ctx.User, PlatformPermissions.LessonsManage)))
            .AddPolicy("EquipmentAccess", policy => policy.RequireAssertion(ctx => HasModuleAccess(ctx.User, PlatformPermissions.EquipmentManage)))
            .AddPolicy("MaintenanceAccess", policy => policy.RequireAssertion(ctx => HasModuleAccess(ctx.User, PlatformPermissions.MaintenanceManage)))
            .AddPolicy("FinanceAccess", policy => policy.RequireAssertion(ctx => HasModuleAccess(ctx.User, PlatformPermissions.FinanceManage)))
            .AddPolicy("SchoolManagementAccess", policy => policy.RequireAssertion(ctx => HasModuleAccess(ctx.User, PlatformPermissions.SchoolManage)))
            .AddPolicy("AuthenticatedUsers", policy => policy.RequireAuthenticatedUser());

        return services;
    }

    private static bool HasModuleAccess(System.Security.Claims.ClaimsPrincipal user, string permission)
        => user.IsInRole("SystemAdmin") ||
           user.IsInRole("Owner") ||
           PlatformPermissions.HasPermission(user, permission);
}
