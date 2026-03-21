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
    await dbContext.Database.ExecuteSqlRawAsync("""
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name = 'courses' AND column_name = 'TotalLessons'
    ) THEN
        UPDATE courses
        SET "TotalMinutes" = COALESCE("TotalMinutes", GREATEST("TotalLessons", 0) * 60)
        WHERE "TotalMinutes" IS NULL;
    END IF;
END $$;
""");
    await dbContext.Database.ExecuteSqlRawAsync("""
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name = 'enrollments' AND column_name = 'IncludedLessonsSnapshot'
    ) THEN
        UPDATE enrollments
        SET "IncludedMinutesSnapshot" = COALESCE("IncludedMinutesSnapshot", GREATEST("IncludedLessonsSnapshot", 0) * 60)
        WHERE "IncludedMinutesSnapshot" IS NULL;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name = 'enrollments' AND column_name = 'UsedLessons'
    ) THEN
        UPDATE enrollments
        SET "UsedMinutes" = COALESCE("UsedMinutes", GREATEST("UsedLessons", 0) * 60)
        WHERE "UsedMinutes" IS NULL;
    END IF;
END $$;
""");
    await dbContext.Database.ExecuteSqlRawAsync("""
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name = 'enrollment_balance_ledger' AND column_name = 'DeltaLessons'
    ) THEN
        UPDATE enrollment_balance_ledger
        SET "DeltaMinutes" = COALESCE("DeltaMinutes", "DeltaLessons" * 60)
        WHERE "DeltaMinutes" IS NULL;
    END IF;
END $$;
""");
    await dbContext.Database.ExecuteSqlRawAsync("""
DROP INDEX IF EXISTS "IX_students_SchoolId_IdentityUserId";
""");
    await dbContext.Database.ExecuteSqlRawAsync("""
CREATE UNIQUE INDEX IF NOT EXISTS "IX_students_SchoolId_IdentityUserId_UQ"
ON students ("SchoolId", "IdentityUserId")
WHERE "IdentityUserId" IS NOT NULL;
""");
    await dbContext.Database.ExecuteSqlRawAsync("""
ALTER TABLE instructors
ADD COLUMN IF NOT EXISTS "AvailabilityJson" character varying(4000) NULL;

ALTER TABLE lessons
ADD COLUMN IF NOT EXISTS "OperationalConfirmedAtUtc" timestamp with time zone NULL;

ALTER TABLE lessons
ADD COLUMN IF NOT EXISTS "OperationalConfirmedByUserId" uuid NULL;

ALTER TABLE lessons
ADD COLUMN IF NOT EXISTS "OperationalConfirmationNote" character varying(1000) NULL;

ALTER TABLE lessons
ADD COLUMN IF NOT EXISTS "NoShowMarkedAtUtc" timestamp with time zone NULL;

ALTER TABLE lessons
ADD COLUMN IF NOT EXISTS "NoShowMarkedByUserId" uuid NULL;

ALTER TABLE lessons
ADD COLUMN IF NOT EXISTS "NoShowNote" character varying(1000) NULL;
""");
    await dbContext.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS schedule_blocks
(
    "Id" uuid NOT NULL,
    "SchoolId" uuid NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "Scope" integer NOT NULL,
    "InstructorId" uuid NULL,
    "Title" character varying(160) NOT NULL,
    "Notes" character varying(1000) NULL,
    "StartAtUtc" timestamp with time zone NOT NULL,
    "EndAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_schedule_blocks" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_schedule_blocks_instructors_InstructorId" FOREIGN KEY ("InstructorId") REFERENCES instructors ("Id") ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS "IX_schedule_blocks_SchoolId_StartAtUtc_EndAtUtc"
ON schedule_blocks ("SchoolId", "StartAtUtc", "EndAtUtc");

CREATE INDEX IF NOT EXISTS "IX_schedule_blocks_SchoolId_InstructorId_StartAtUtc_EndAtUtc"
ON schedule_blocks ("SchoolId", "InstructorId", "StartAtUtc", "EndAtUtc");
""");
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
