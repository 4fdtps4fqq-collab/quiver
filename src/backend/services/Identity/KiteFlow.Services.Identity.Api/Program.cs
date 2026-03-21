using KiteFlow.BuildingBlocks.Authentication;
using KiteFlow.BuildingBlocks.OpenApi;
using KiteFlow.BuildingBlocks.Web;
using KiteFlow.Services.Identity.Api.Configuration;
using KiteFlow.Services.Identity.Api.Data;
using KiteFlow.Services.Identity.Api.Domain;
using KiteFlow.Services.Identity.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureKiteFlowLogging();
builder.Services.AddControllers();
builder.Services.AddKiteFlowWebInfrastructure();
builder.Services.AddKiteFlowPlatformAuthentication(builder.Configuration);
builder.Services.AddKiteFlowSwagger("KiteFlow Identity Service");
builder.Services
    .AddOptions<SystemAdminBootstrapOptions>()
    .Bind(builder.Configuration.GetSection(SystemAdminBootstrapOptions.SectionName));
builder.Services
    .AddOptions<IdentityEmailDeliveryOptions>()
    .Bind(builder.Configuration.GetSection(IdentityEmailDeliveryOptions.SectionName));

builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Database")));
builder.Services.AddScoped<IIdentityEmailDeliveryService, IdentityEmailDeliveryService>();
builder.Services.AddScoped<AuthenticationAuditService>();
builder.Services.AddKiteFlowDownstreamClient("schools", builder.Configuration, "DownstreamServices:Schools");

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    var bootstrapOptions = scope.ServiceProvider.GetRequiredService<IOptions<SystemAdminBootstrapOptions>>().Value;

    await dbContext.Database.MigrateAsync();
    await dbContext.Database.ExecuteSqlRawAsync("""
        ALTER TABLE user_accounts
        ADD COLUMN IF NOT EXISTS "PermissionsJson" text NULL;
        """);
    await dbContext.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS password_reset_tokens (
            "Id" uuid PRIMARY KEY,
            "UserAccountId" uuid NOT NULL REFERENCES user_accounts("Id") ON DELETE CASCADE,
            "TokenHash" character varying(128) NOT NULL,
            "ExpiresAtUtc" timestamp with time zone NOT NULL,
            "CreatedAtUtc" timestamp with time zone NOT NULL,
            "UsedAtUtc" timestamp with time zone NULL,
            "RequestedIpAddress" character varying(120) NULL,
            "RequestedUserAgent" character varying(500) NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_password_reset_tokens_TokenHash" ON password_reset_tokens ("TokenHash");
        CREATE INDEX IF NOT EXISTS "IX_password_reset_tokens_UserAccountId_ExpiresAtUtc" ON password_reset_tokens ("UserAccountId", "ExpiresAtUtc");
        """);
    await dbContext.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS authentication_audit_events (
            "Id" uuid PRIMARY KEY,
            "SchoolId" uuid NULL,
            "UserAccountId" uuid NULL,
            "TargetUserAccountId" uuid NULL,
            "EventType" character varying(80) NOT NULL,
            "Outcome" character varying(40) NOT NULL,
            "Email" character varying(320) NULL,
            "IpAddress" character varying(120) NULL,
            "UserAgent" character varying(500) NULL,
            "MetadataJson" text NULL,
            "CreatedAtUtc" timestamp with time zone NOT NULL
        );
        CREATE INDEX IF NOT EXISTS "IX_authentication_audit_events_SchoolId_CreatedAtUtc" ON authentication_audit_events ("SchoolId", "CreatedAtUtc");
        CREATE INDEX IF NOT EXISTS "IX_authentication_audit_events_UserAccountId_CreatedAtUtc" ON authentication_audit_events ("UserAccountId", "CreatedAtUtc");
        CREATE INDEX IF NOT EXISTS "IX_authentication_audit_events_TargetUserAccountId_CreatedAtUtc" ON authentication_audit_events ("TargetUserAccountId", "CreatedAtUtc");
        """);

    if (bootstrapOptions.Enabled &&
        !string.IsNullOrWhiteSpace(bootstrapOptions.Email) &&
        !string.IsNullOrWhiteSpace(bootstrapOptions.Password))
    {
        var email = bootstrapOptions.Email.Trim().ToLowerInvariant();
        var systemAdminExists = await dbContext.UserAccounts.AnyAsync(x => x.Email == email);

        if (!systemAdminExists)
        {
            dbContext.UserAccounts.Add(new UserAccount
            {
                SchoolId = null,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(bootstrapOptions.Password),
                Role = PlatformRole.SystemAdmin,
                MustChangePassword = false
            });

            await dbContext.SaveChangesAsync();
        }
    }
}

app.UseKiteFlowDefaults();
app.MapControllers();

app.MapGet("/", () => Results.Ok(new
{
    service = "identity",
    status = "ok",
    docs = "/swagger"
}));

app.Run();

public partial class Program;
