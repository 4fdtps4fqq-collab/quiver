using KiteFlow.BuildingBlocks.Authentication;
using KiteFlow.BuildingBlocks.OpenApi;
using KiteFlow.BuildingBlocks.Web;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureKiteFlowLogging();
builder.Services.AddControllers();
builder.Services.AddKiteFlowWebInfrastructure();
builder.Services.AddKiteFlowPlatformAuthentication(builder.Configuration);
builder.Services.AddKiteFlowSwagger("KiteFlow Reporting Service");
builder.Services.AddMemoryCache();
builder.Services.AddKiteFlowDownstreamClient("academics", builder.Configuration, "DownstreamServices:Academics");
builder.Services.AddKiteFlowDownstreamClient("equipment", builder.Configuration, "DownstreamServices:Equipment");
builder.Services.AddKiteFlowDownstreamClient("finance", builder.Configuration, "DownstreamServices:Finance");

var app = builder.Build();

app.UseKiteFlowDefaults();
app.MapControllers();

app.MapGet("/", () => Results.Ok(new
{
    service = "reporting",
    status = "ok",
    docs = "/swagger"
}));

app.Run();

public partial class Program;
