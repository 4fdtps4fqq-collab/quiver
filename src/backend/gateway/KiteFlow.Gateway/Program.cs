using KiteFlow.BuildingBlocks.Authentication;
using KiteFlow.BuildingBlocks.OpenApi;
using KiteFlow.BuildingBlocks.Web;
using KiteFlow.Gateway.Configuration;
using KiteFlow.Gateway.Middleware;
using KiteFlow.Gateway.Services;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureKiteFlowLogging();
builder.Services.AddControllers();
builder.Services.AddKiteFlowWebInfrastructure();
builder.Services.AddKiteFlowPlatformAuthentication(builder.Configuration);
builder.Services.AddKiteFlowSwagger("KiteFlow Gateway");
builder.Services.AddOptions<OwnerCredentialDeliveryOptions>()
    .Bind(builder.Configuration.GetSection(OwnerCredentialDeliveryOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddScoped<IOwnerCredentialDeliveryService, OwnerCredentialDeliveryService>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:5174")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddKiteFlowDownstreamClient("identity", builder.Configuration, "DownstreamServices:Identity");
builder.Services.AddKiteFlowDownstreamClient("schools", builder.Configuration, "DownstreamServices:Schools");
builder.Services.AddKiteFlowDownstreamClient("academics", builder.Configuration, "DownstreamServices:Academics");
builder.Services.AddKiteFlowDownstreamClient("equipment", builder.Configuration, "DownstreamServices:Equipment");
builder.Services.AddKiteFlowDownstreamClient("finance", builder.Configuration, "DownstreamServices:Finance");
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseCors("frontend");
app.UseKiteFlowDefaults();
app.UseMiddleware<SchoolAvailabilityMiddleware>();
app.MapControllers();

app.MapGet("/", () => Results.Ok(new
{
    gateway = "kiteflow",
    status = "ok",
    routes = new[]
    {
        "/identity/{**catch-all}",
        "/schools/{**catch-all}",
        "/academics/{**catch-all}",
        "/equipment/{**catch-all}",
        "/finance/{**catch-all}",
        "/reporting/{**catch-all}"
    }
}));

app.MapReverseProxy();

app.Run();

public partial class Program;
