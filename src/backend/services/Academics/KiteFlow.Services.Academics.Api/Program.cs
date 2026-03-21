using KiteFlow.BuildingBlocks.Authentication;
using KiteFlow.BuildingBlocks.OpenApi;
using KiteFlow.BuildingBlocks.Web;
using KiteFlow.Services.Academics.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureKiteFlowLogging();
builder.Services.AddControllers();
builder.Services.AddKiteFlowWebInfrastructure();
builder.Services.AddKiteFlowPlatformAuthentication(builder.Configuration);
builder.Services.AddKiteFlowSwagger("KiteFlow Academics Service");
builder.Services.AddDbContext<AcademicsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Database")));
builder.Services.AddKiteFlowDownstreamClient("schools", builder.Configuration, "DownstreamServices:Schools");
builder.Services.AddKiteFlowDownstreamClient("finance", builder.Configuration, "DownstreamServices:Finance");
builder.Services.AddScoped<KiteFlow.Services.Academics.Api.Services.SchoolOperationsSettingsClient>();
builder.Services.AddScoped<KiteFlow.Services.Academics.Api.Services.LessonSchedulingService>();
builder.Services.AddScoped<KiteFlow.Services.Academics.Api.Services.FinancialAutomationService>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AcademicsDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseKiteFlowDefaults();
app.MapControllers();

app.MapGet("/", () => Results.Ok(new
{
    service = "academics",
    status = "ok",
    docs = "/swagger"
}));

app.Run();

public partial class Program;
