using KiteFlow.BuildingBlocks.Authentication;
using KiteFlow.BuildingBlocks.OpenApi;
using KiteFlow.BuildingBlocks.Web;
using KiteFlow.Services.Schools.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureKiteFlowLogging();
builder.Services.AddControllers();
builder.Services.AddKiteFlowPlatformAuthentication(builder.Configuration);
builder.Services.AddKiteFlowSwagger("KiteFlow Schools Service");
builder.Services.AddDbContext<SchoolsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Database")));

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SchoolsDbContext>();
    await dbContext.Database.MigrateAsync();
    await dbContext.Database.ExecuteSqlRawAsync("""
ALTER TABLE schools
ADD COLUMN IF NOT EXISTS "LogoDataUrl" text NULL;

ALTER TABLE schools
ADD COLUMN IF NOT EXISTS "Cnpj" character varying(18) NULL;

ALTER TABLE schools
ADD COLUMN IF NOT EXISTS "BaseBeachName" character varying(160) NULL;

ALTER TABLE schools
ADD COLUMN IF NOT EXISTS "BaseLatitude" double precision NULL;

ALTER TABLE schools
ADD COLUMN IF NOT EXISTS "BaseLongitude" double precision NULL;

ALTER TABLE schools
ADD COLUMN IF NOT EXISTS "PostalCode" character varying(9) NULL;

ALTER TABLE schools
ADD COLUMN IF NOT EXISTS "Street" character varying(200) NULL;

ALTER TABLE schools
ADD COLUMN IF NOT EXISTS "StreetNumber" character varying(20) NULL;

ALTER TABLE schools
ADD COLUMN IF NOT EXISTS "AddressComplement" character varying(120) NULL;

ALTER TABLE schools
ADD COLUMN IF NOT EXISTS "Neighborhood" character varying(120) NULL;

ALTER TABLE schools
ADD COLUMN IF NOT EXISTS "City" character varying(120) NULL;

ALTER TABLE schools
ADD COLUMN IF NOT EXISTS "State" character varying(2) NULL;

ALTER TABLE school_settings
ADD COLUMN IF NOT EXISTS "RescheduleWindowHours" integer NOT NULL DEFAULT 24;

ALTER TABLE school_settings
ADD COLUMN IF NOT EXISTS "AttendanceConfirmationLeadMinutes" integer NOT NULL DEFAULT 180;

ALTER TABLE school_settings
ADD COLUMN IF NOT EXISTS "LessonReminderLeadHours" integer NOT NULL DEFAULT 18;

ALTER TABLE school_settings
ADD COLUMN IF NOT EXISTS "PortalNotificationsEnabled" boolean NOT NULL DEFAULT TRUE;

ALTER TABLE school_settings
ADD COLUMN IF NOT EXISTS "InstructorBufferMinutes" integer NOT NULL DEFAULT 15;

ALTER TABLE school_settings
ADD COLUMN IF NOT EXISTS "NoShowGraceMinutes" integer NOT NULL DEFAULT 15;

ALTER TABLE school_settings
ADD COLUMN IF NOT EXISTS "NoShowConsumesCourseMinutes" boolean NOT NULL DEFAULT TRUE;

ALTER TABLE school_settings
ADD COLUMN IF NOT EXISTS "NoShowChargesSingleLesson" boolean NOT NULL DEFAULT TRUE;

ALTER TABLE school_settings
ADD COLUMN IF NOT EXISTS "AutoCreateEnrollmentRevenue" boolean NOT NULL DEFAULT TRUE;

ALTER TABLE school_settings
ADD COLUMN IF NOT EXISTS "AutoCreateSingleLessonRevenue" boolean NOT NULL DEFAULT TRUE;

ALTER TABLE user_profiles
ADD COLUMN IF NOT EXISTS "Cpf" character varying(14) NULL;

ALTER TABLE user_profiles
ADD COLUMN IF NOT EXISTS "PostalCode" character varying(9) NULL;

ALTER TABLE user_profiles
ADD COLUMN IF NOT EXISTS "Street" character varying(200) NULL;

ALTER TABLE user_profiles
ADD COLUMN IF NOT EXISTS "StreetNumber" character varying(20) NULL;

ALTER TABLE user_profiles
ADD COLUMN IF NOT EXISTS "AddressComplement" character varying(120) NULL;

ALTER TABLE user_profiles
ADD COLUMN IF NOT EXISTS "Neighborhood" character varying(120) NULL;

ALTER TABLE user_profiles
ADD COLUMN IF NOT EXISTS "City" character varying(120) NULL;

ALTER TABLE user_profiles
ADD COLUMN IF NOT EXISTS "State" character varying(2) NULL;
""");
}

app.UseKiteFlowDefaults();
app.MapControllers();

app.MapGet("/", () => Results.Ok(new
{
    service = "schools",
    status = "ok",
    docs = "/swagger"
}));

app.Run();

public partial class Program;
