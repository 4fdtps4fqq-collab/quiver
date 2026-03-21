using KiteFlow.BuildingBlocks.Authentication;
using KiteFlow.BuildingBlocks.OpenApi;
using KiteFlow.BuildingBlocks.Web;
using KiteFlow.Services.Equipment.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureKiteFlowLogging();
builder.Services.AddControllers();
builder.Services.AddKiteFlowWebInfrastructure();
builder.Services.AddKiteFlowPlatformAuthentication(builder.Configuration);
builder.Services.AddKiteFlowSwagger("KiteFlow Equipment Service");
builder.Services.AddKiteFlowDownstreamClient("academics", builder.Configuration, "DownstreamServices:Academics");
builder.Services.AddKiteFlowDownstreamClient("finance", builder.Configuration, "DownstreamServices:Finance");
builder.Services.AddDbContext<EquipmentDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Database")));

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<EquipmentDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseKiteFlowDefaults();
app.MapControllers();

app.MapGet("/", () => Results.Ok(new
{
    service = "equipment",
    status = "ok",
    docs = "/swagger"
}));

app.Run();

public partial class Program;
