using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KiteFlow.Services.Equipment.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gear_storages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LocationNote = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gear_storages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "lesson_equipment_checkouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckedOutAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CheckedInAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckedInByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    NotesBefore = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    NotesAfter = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lesson_equipment_checkouts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "maintenance_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EquipmentType = table.Column<int>(type: "integer", nullable: false),
                    ServiceEveryMinutes = table.Column<int>(type: "integer", nullable: true),
                    ServiceEveryDays = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "equipment_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    TagCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Brand = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SizeLabel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CurrentCondition = table.Column<int>(type: "integer", nullable: false),
                    TotalUsageMinutes = table.Column<int>(type: "integer", nullable: false),
                    LastServiceDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastServiceUsageMinutes = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipment_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_equipment_items_gear_storages_StorageId",
                        column: x => x.StorageId,
                        principalTable: "gear_storages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "equipment_usage_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckoutItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsageMinutes = table.Column<int>(type: "integer", nullable: false),
                    ConditionAfter = table.Column<int>(type: "integer", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipment_usage_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_equipment_usage_logs_equipment_items_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "equipment_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lesson_equipment_checkout_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckoutId = table.Column<Guid>(type: "uuid", nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConditionBefore = table.Column<int>(type: "integer", nullable: false),
                    ConditionAfter = table.Column<int>(type: "integer", nullable: true),
                    NotesBefore = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    NotesAfter = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lesson_equipment_checkout_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lesson_equipment_checkout_items_equipment_items_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "equipment_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lesson_equipment_checkout_items_lesson_equipment_checkouts_~",
                        column: x => x.CheckoutId,
                        principalTable: "lesson_equipment_checkouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "maintenance_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsageMinutesAtService = table.Column<int>(type: "integer", nullable: false),
                    Cost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PerformedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_maintenance_records_equipment_items_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "equipment_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_equipment_items_SchoolId_TagCode",
                table: "equipment_items",
                columns: new[] { "SchoolId", "TagCode" });

            migrationBuilder.CreateIndex(
                name: "IX_equipment_items_SchoolId_Type",
                table: "equipment_items",
                columns: new[] { "SchoolId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_equipment_items_StorageId",
                table: "equipment_items",
                column: "StorageId");

            migrationBuilder.CreateIndex(
                name: "IX_equipment_usage_logs_EquipmentId",
                table: "equipment_usage_logs",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_equipment_usage_logs_SchoolId_EquipmentId_RecordedAtUtc",
                table: "equipment_usage_logs",
                columns: new[] { "SchoolId", "EquipmentId", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_gear_storages_SchoolId_Name",
                table: "gear_storages",
                columns: new[] { "SchoolId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lesson_equipment_checkout_items_CheckoutId",
                table: "lesson_equipment_checkout_items",
                column: "CheckoutId");

            migrationBuilder.CreateIndex(
                name: "IX_lesson_equipment_checkout_items_EquipmentId",
                table: "lesson_equipment_checkout_items",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_lesson_equipment_checkout_items_SchoolId_CheckoutId_Equipme~",
                table: "lesson_equipment_checkout_items",
                columns: new[] { "SchoolId", "CheckoutId", "EquipmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lesson_equipment_checkouts_SchoolId_LessonId",
                table: "lesson_equipment_checkouts",
                columns: new[] { "SchoolId", "LessonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_records_EquipmentId",
                table: "maintenance_records",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_records_SchoolId_ServiceDateUtc",
                table: "maintenance_records",
                columns: new[] { "SchoolId", "ServiceDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_rules_SchoolId_EquipmentType",
                table: "maintenance_rules",
                columns: new[] { "SchoolId", "EquipmentType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "equipment_usage_logs");

            migrationBuilder.DropTable(
                name: "lesson_equipment_checkout_items");

            migrationBuilder.DropTable(
                name: "maintenance_records");

            migrationBuilder.DropTable(
                name: "maintenance_rules");

            migrationBuilder.DropTable(
                name: "lesson_equipment_checkouts");

            migrationBuilder.DropTable(
                name: "equipment_items");

            migrationBuilder.DropTable(
                name: "gear_storages");
        }
    }
}
