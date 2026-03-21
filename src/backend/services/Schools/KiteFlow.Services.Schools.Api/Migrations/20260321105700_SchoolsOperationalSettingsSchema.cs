using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KiteFlow.Services.Schools.Api.Migrations
{
    /// <inheritdoc />
    public partial class SchoolsOperationalSettingsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
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
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intencionalmente vazio para evitar remoções destrutivas em bancos já sincronizados.
        }
    }
}
