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
