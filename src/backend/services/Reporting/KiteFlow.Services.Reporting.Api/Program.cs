using KiteFlow.BuildingBlocks.Authentication;
using KiteFlow.BuildingBlocks.OpenApi;
using KiteFlow.BuildingBlocks.Web;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureKiteFlowLogging();
builder.Services.AddControllers();
builder.Services.AddKiteFlowPlatformAuthentication(builder.Configuration);
builder.Services.AddKiteFlowSwagger("KiteFlow Reporting Service");
builder.Services.AddHttpClient("academics", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["DownstreamServices:Academics"]!);
});
builder.Services.AddHttpClient("equipment", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["DownstreamServices:Equipment"]!);
});
builder.Services.AddHttpClient("finance", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["DownstreamServices:Finance"]!);
});

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
