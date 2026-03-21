using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KiteFlow.Services.Academics.Api.Migrations
{
    /// <inheritdoc />
    public partial class CourseLevelSettingsCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "course_level_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LevelValue = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    PedagogicalTrackJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_course_level_settings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_course_level_settings_SchoolId_LevelValue",
                table: "course_level_settings",
                columns: new[] { "SchoolId", "LevelValue" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_course_level_settings_SchoolId_SortOrder",
                table: "course_level_settings",
                columns: new[] { "SchoolId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "course_level_settings");
        }
    }
}
