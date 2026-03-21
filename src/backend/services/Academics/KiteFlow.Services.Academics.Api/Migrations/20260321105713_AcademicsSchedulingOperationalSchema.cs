using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KiteFlow.Services.Academics.Api.Migrations
{
    /// <inheritdoc />
    public partial class AcademicsSchedulingOperationalSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
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

            migrationBuilder.Sql("""
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

CREATE INDEX IF NOT EXISTS "IX_schedule_blocks_InstructorId"
ON schedule_blocks ("InstructorId");

CREATE INDEX IF NOT EXISTS "IX_schedule_blocks_SchoolId_StartAtUtc_EndAtUtc"
ON schedule_blocks ("SchoolId", "StartAtUtc", "EndAtUtc");

CREATE INDEX IF NOT EXISTS "IX_schedule_blocks_SchoolId_InstructorId_StartAtUtc_EndAtUtc"
ON schedule_blocks ("SchoolId", "InstructorId", "StartAtUtc", "EndAtUtc");
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intencionalmente vazio para evitar remoções destrutivas em bancos já sincronizados.
        }
    }
}
