using KiteFlow.BuildingBlocks.Authentication;
using KiteFlow.BuildingBlocks.OpenApi;
using KiteFlow.BuildingBlocks.Web;
using KiteFlow.Services.Equipment.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureKiteFlowLogging();
builder.Services.AddControllers();
builder.Services.AddKiteFlowPlatformAuthentication(builder.Configuration);
builder.Services.AddKiteFlowSwagger("KiteFlow Equipment Service");
builder.Services.AddHttpClient("academics", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["DownstreamServices:Academics"]!);
});
builder.Services.AddHttpClient("finance", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["DownstreamServices:Finance"]!);
});
builder.Services.AddDbContext<EquipmentDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Database")));

var app = builder.Build();

await EnsureEquipmentRuntimeSchemaAsync(app.Services);

app.UseKiteFlowDefaults();
app.MapControllers();

app.MapGet("/", () => Results.Ok(new
{
    service = "equipment",
    status = "ok",
    docs = "/swagger"
}));

app.Run();

static async Task EnsureEquipmentRuntimeSchemaAsync(IServiceProvider services)
{
    await using var scope = services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<EquipmentDbContext>();

    var commands = new[]
    {
        """
        ALTER TABLE IF EXISTS equipment_items
            ADD COLUMN IF NOT EXISTS "Category" character varying(120),
            ADD COLUMN IF NOT EXISTS "OwnershipType" integer NOT NULL DEFAULT 1,
            ADD COLUMN IF NOT EXISTS "OwnerDisplayName" character varying(200);
        """,
        """
        ALTER TABLE IF EXISTS maintenance_rules
            ADD COLUMN IF NOT EXISTS "PlanName" character varying(150) NOT NULL DEFAULT 'Plano preventivo',
            ADD COLUMN IF NOT EXISTS "ServiceCategory" integer NOT NULL DEFAULT 1,
            ADD COLUMN IF NOT EXISTS "WarningLeadMinutes" integer,
            ADD COLUMN IF NOT EXISTS "CriticalLeadMinutes" integer,
            ADD COLUMN IF NOT EXISTS "WarningLeadDays" integer,
            ADD COLUMN IF NOT EXISTS "CriticalLeadDays" integer,
            ADD COLUMN IF NOT EXISTS "Checklist" character varying(2000),
            ADD COLUMN IF NOT EXISTS "Notes" character varying(2000);
        """,
        """
        ALTER TABLE IF EXISTS maintenance_records
            ADD COLUMN IF NOT EXISTS "ServiceCategory" integer NOT NULL DEFAULT 1,
            ADD COLUMN IF NOT EXISTS "FinancialEffect" integer NOT NULL DEFAULT 0,
            ADD COLUMN IF NOT EXISTS "CounterpartyName" character varying(200);
        """,
        """
        CREATE TABLE IF NOT EXISTS equipment_reservations (
            "Id" uuid PRIMARY KEY,
            "SchoolId" uuid NOT NULL,
            "LessonId" uuid NOT NULL,
            "ReservedFromUtc" timestamp with time zone NOT NULL,
            "ReservedUntilUtc" timestamp with time zone NOT NULL,
            "Notes" character varying(1000),
            "CreatedByUserId" uuid NOT NULL,
            "CreatedAtUtc" timestamp with time zone NOT NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS equipment_reservation_items (
            "Id" uuid PRIMARY KEY,
            "SchoolId" uuid NOT NULL,
            "ReservationId" uuid NOT NULL REFERENCES equipment_reservations("Id") ON DELETE CASCADE,
            "EquipmentId" uuid NOT NULL REFERENCES equipment_items("Id") ON DELETE RESTRICT,
            "CreatedAtUtc" timestamp with time zone NOT NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS equipment_kits (
            "Id" uuid PRIMARY KEY,
            "SchoolId" uuid NOT NULL,
            "Name" character varying(200) NOT NULL,
            "Description" character varying(1000),
            "IsActive" boolean NOT NULL,
            "CreatedAtUtc" timestamp with time zone NOT NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS equipment_kit_items (
            "Id" uuid PRIMARY KEY,
            "SchoolId" uuid NOT NULL,
            "KitId" uuid NOT NULL REFERENCES equipment_kits("Id") ON DELETE CASCADE,
            "EquipmentId" uuid NOT NULL REFERENCES equipment_items("Id") ON DELETE RESTRICT,
            "CreatedAtUtc" timestamp with time zone NOT NULL
        );
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_equipment_reservations_SchoolId_LessonId" ON equipment_reservations ("SchoolId", "LessonId");""",
        """CREATE INDEX IF NOT EXISTS "IX_equipment_reservations_SchoolId_ReservedFromUtc_ReservedUntilUtc" ON equipment_reservations ("SchoolId", "ReservedFromUtc", "ReservedUntilUtc");""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_equipment_reservation_items_SchoolId_ReservationId_EquipmentId" ON equipment_reservation_items ("SchoolId", "ReservationId", "EquipmentId");""",
        """CREATE INDEX IF NOT EXISTS "IX_equipment_reservation_items_SchoolId_EquipmentId" ON equipment_reservation_items ("SchoolId", "EquipmentId");""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_equipment_kits_SchoolId_Name" ON equipment_kits ("SchoolId", "Name");""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_equipment_kit_items_SchoolId_KitId_EquipmentId" ON equipment_kit_items ("SchoolId", "KitId", "EquipmentId");""",
        """CREATE INDEX IF NOT EXISTS "IX_equipment_items_SchoolId_Category" ON equipment_items ("SchoolId", "Category");"""
    };

    foreach (var command in commands)
    {
        await dbContext.Database.ExecuteSqlRawAsync(command);
    }
}

public partial class Program;
