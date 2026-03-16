using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KiteFlow.Services.Academics.Api.Migrations
{
    public partial class AcademicsRuntimeSchemaSync : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'courses' AND column_name = 'TotalLessons'
    ) AND NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'courses' AND column_name = 'TotalMinutes'
    ) THEN
        ALTER TABLE courses RENAME COLUMN "TotalLessons" TO "TotalMinutes";
    END IF;
END $$;
""");

            migrationBuilder.Sql("""
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'enrollments' AND column_name = 'IncludedLessonsSnapshot'
    ) AND NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'enrollments' AND column_name = 'IncludedMinutesSnapshot'
    ) THEN
        ALTER TABLE enrollments RENAME COLUMN "IncludedLessonsSnapshot" TO "IncludedMinutesSnapshot";
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'enrollments' AND column_name = 'UsedLessons'
    ) AND NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'enrollments' AND column_name = 'UsedMinutes'
    ) THEN
        ALTER TABLE enrollments RENAME COLUMN "UsedLessons" TO "UsedMinutes";
    END IF;
END $$;
""");

            migrationBuilder.Sql("""
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'enrollment_balance_ledger' AND column_name = 'DeltaLessons'
    ) AND NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'enrollment_balance_ledger' AND column_name = 'DeltaMinutes'
    ) THEN
        ALTER TABLE enrollment_balance_ledger RENAME COLUMN "DeltaLessons" TO "DeltaMinutes";
    END IF;
END $$;
""");

            migrationBuilder.Sql("""
ALTER TABLE students ADD COLUMN IF NOT EXISTS "IdentityUserId" uuid NULL;
ALTER TABLE students ADD COLUMN IF NOT EXISTS "PostalCode" character varying(20) NULL;
ALTER TABLE students ADD COLUMN IF NOT EXISTS "Street" character varying(200) NULL;
ALTER TABLE students ADD COLUMN IF NOT EXISTS "StreetNumber" character varying(30) NULL;
ALTER TABLE students ADD COLUMN IF NOT EXISTS "AddressComplement" character varying(120) NULL;
ALTER TABLE students ADD COLUMN IF NOT EXISTS "Neighborhood" character varying(120) NULL;
ALTER TABLE students ADD COLUMN IF NOT EXISTS "City" character varying(120) NULL;
ALTER TABLE students ADD COLUMN IF NOT EXISTS "State" character varying(80) NULL;

ALTER TABLE instructors ADD COLUMN IF NOT EXISTS "HourlyRate" numeric(12,2) NOT NULL DEFAULT 0;

ALTER TABLE lessons ADD COLUMN IF NOT EXISTS "StudentConfirmedAtUtc" timestamp with time zone NULL;
ALTER TABLE lessons ADD COLUMN IF NOT EXISTS "StudentConfirmationNote" character varying(1000) NULL;
""");

            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS student_portal_notifications
(
    "Id" uuid NOT NULL,
    "SchoolId" uuid NOT NULL,
    "StudentId" uuid NOT NULL,
    "LessonId" uuid NULL,
    "Category" character varying(50) NOT NULL,
    "Title" character varying(160) NOT NULL,
    "Message" character varying(1500) NOT NULL,
    "ActionLabel" character varying(80) NULL,
    "ActionPath" character varying(250) NULL,
    "ReadAtUtc" timestamp with time zone NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_student_portal_notifications" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_students_SchoolId_IdentityUserId"
ON students ("SchoolId", "IdentityUserId")
WHERE "IdentityUserId" IS NOT NULL;

CREATE INDEX IF NOT EXISTS "IX_student_portal_notifications_SchoolId_StudentId_CreatedAtUtc"
ON student_portal_notifications ("SchoolId", "StudentId", "CreatedAtUtc");

CREATE INDEX IF NOT EXISTS "IX_student_portal_notifications_SchoolId_StudentId_ReadAtUtc"
ON student_portal_notifications ("SchoolId", "StudentId", "ReadAtUtc");
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intencionalmente vazio para evitar remoções destrutivas em bancos já sincronizados.
        }
    }
}
