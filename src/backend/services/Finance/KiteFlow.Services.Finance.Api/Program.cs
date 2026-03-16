using KiteFlow.BuildingBlocks.Authentication;
using KiteFlow.BuildingBlocks.OpenApi;
using KiteFlow.BuildingBlocks.Web;
using KiteFlow.Services.Finance.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureKiteFlowLogging();
builder.Services.AddControllers();
builder.Services.AddKiteFlowPlatformAuthentication(builder.Configuration);
builder.Services.AddKiteFlowSwagger("KiteFlow Finance Service");
builder.Services.AddHttpClient("academics", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["DownstreamServices:Academics"]!);
});
builder.Services.AddDbContext<FinanceDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Database")));

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
    await dbContext.Database.MigrateAsync();
    await dbContext.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS accounts_receivable_entries
(
    "Id" uuid NOT NULL,
    "SchoolId" uuid NOT NULL,
    "StudentId" uuid NOT NULL,
    "EnrollmentId" uuid NULL,
    "StudentNameSnapshot" character varying(200) NOT NULL,
    "Description" character varying(500) NOT NULL,
    "Notes" character varying(1000) NULL,
    "Amount" numeric(12,2) NOT NULL,
    "PaidAmount" numeric(12,2) NOT NULL DEFAULT 0,
    "DueAtUtc" timestamp with time zone NOT NULL,
    "LastPaymentAtUtc" timestamp with time zone NULL,
    "Status" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_accounts_receivable_entries" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_accounts_receivable_entries_SchoolId_StudentId_DueAtUtc"
ON accounts_receivable_entries ("SchoolId", "StudentId", "DueAtUtc");

CREATE INDEX IF NOT EXISTS "IX_accounts_receivable_entries_SchoolId_Status_DueAtUtc"
ON accounts_receivable_entries ("SchoolId", "Status", "DueAtUtc");
""");

    await dbContext.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS accounts_receivable_payments
(
    "Id" uuid NOT NULL,
    "SchoolId" uuid NOT NULL,
    "ReceivableId" uuid NOT NULL,
    "Amount" numeric(12,2) NOT NULL,
    "PaidAtUtc" timestamp with time zone NOT NULL,
    "Note" character varying(500) NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_accounts_receivable_payments" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_accounts_receivable_payments_SchoolId_ReceivableId_PaidAtUtc"
ON accounts_receivable_payments ("SchoolId", "ReceivableId", "PaidAtUtc");
""");
    await dbContext.Database.ExecuteSqlRawAsync("""
CREATE UNIQUE INDEX IF NOT EXISTS "IX_revenue_entries_SchoolId_SourceType_SourceId_UQ"
ON revenue_entries ("SchoolId", "SourceType", "SourceId")
WHERE "SourceId" <> '00000000-0000-0000-0000-000000000000';
""");
}

app.UseKiteFlowDefaults();
app.MapControllers();

app.MapGet("/", () => Results.Ok(new
{
    service = "finance",
    status = "ok",
    docs = "/swagger"
}));

app.Run();

public partial class Program;
