using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KiteFlow.Services.Equipment.Api.Migrations
{
    /// <inheritdoc />
    public partial class EquipmentReservationsMaintenanceSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
ALTER TABLE IF EXISTS equipment_items
    ADD COLUMN IF NOT EXISTS "Category" character varying(120),
    ADD COLUMN IF NOT EXISTS "OwnershipType" integer NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS "OwnerDisplayName" character varying(200);
""");

            migrationBuilder.Sql("""
ALTER TABLE IF EXISTS maintenance_rules
    ADD COLUMN IF NOT EXISTS "PlanName" character varying(150) NOT NULL DEFAULT 'Plano preventivo',
    ADD COLUMN IF NOT EXISTS "ServiceCategory" integer NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS "WarningLeadMinutes" integer,
    ADD COLUMN IF NOT EXISTS "CriticalLeadMinutes" integer,
    ADD COLUMN IF NOT EXISTS "WarningLeadDays" integer,
    ADD COLUMN IF NOT EXISTS "CriticalLeadDays" integer,
    ADD COLUMN IF NOT EXISTS "Checklist" character varying(2000),
    ADD COLUMN IF NOT EXISTS "Notes" character varying(2000);
""");

            migrationBuilder.Sql("""
ALTER TABLE IF EXISTS maintenance_records
    ADD COLUMN IF NOT EXISTS "ServiceCategory" integer NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS "FinancialEffect" integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "CounterpartyName" character varying(200);
""");

            migrationBuilder.Sql("""
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
""");

            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS equipment_reservation_items (
    "Id" uuid PRIMARY KEY,
    "SchoolId" uuid NOT NULL,
    "ReservationId" uuid NOT NULL REFERENCES equipment_reservations("Id") ON DELETE CASCADE,
    "EquipmentId" uuid NOT NULL REFERENCES equipment_items("Id") ON DELETE RESTRICT,
    "CreatedAtUtc" timestamp with time zone NOT NULL
);
""");

            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS equipment_kits (
    "Id" uuid PRIMARY KEY,
    "SchoolId" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Description" character varying(1000),
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL
);
""");

            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS equipment_kit_items (
    "Id" uuid PRIMARY KEY,
    "SchoolId" uuid NOT NULL,
    "KitId" uuid NOT NULL REFERENCES equipment_kits("Id") ON DELETE CASCADE,
    "EquipmentId" uuid NOT NULL REFERENCES equipment_items("Id") ON DELETE RESTRICT,
    "CreatedAtUtc" timestamp with time zone NOT NULL
);
""");

            migrationBuilder.Sql("""
CREATE UNIQUE INDEX IF NOT EXISTS "IX_equipment_reservations_SchoolId_LessonId" ON equipment_reservations ("SchoolId", "LessonId");
CREATE INDEX IF NOT EXISTS "IX_equipment_reservations_SchoolId_ReservedFromUtc_ReservedUntilUtc" ON equipment_reservations ("SchoolId", "ReservedFromUtc", "ReservedUntilUtc");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_equipment_reservation_items_SchoolId_ReservationId_EquipmentId" ON equipment_reservation_items ("SchoolId", "ReservationId", "EquipmentId");
CREATE INDEX IF NOT EXISTS "IX_equipment_reservation_items_SchoolId_EquipmentId" ON equipment_reservation_items ("SchoolId", "EquipmentId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_equipment_kits_SchoolId_Name" ON equipment_kits ("SchoolId", "Name");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_equipment_kit_items_SchoolId_KitId_EquipmentId" ON equipment_kit_items ("SchoolId", "KitId", "EquipmentId");
CREATE INDEX IF NOT EXISTS "IX_equipment_kit_items_EquipmentId" ON equipment_kit_items ("EquipmentId");
CREATE INDEX IF NOT EXISTS "IX_equipment_kit_items_KitId" ON equipment_kit_items ("KitId");
CREATE INDEX IF NOT EXISTS "IX_equipment_reservation_items_EquipmentId" ON equipment_reservation_items ("EquipmentId");
CREATE INDEX IF NOT EXISTS "IX_equipment_reservation_items_ReservationId" ON equipment_reservation_items ("ReservationId");
CREATE INDEX IF NOT EXISTS "IX_equipment_items_SchoolId_Category" ON equipment_items ("SchoolId", "Category");
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intencionalmente vazio para evitar remoções destrutivas em bancos já sincronizados.
        }
    }
}
