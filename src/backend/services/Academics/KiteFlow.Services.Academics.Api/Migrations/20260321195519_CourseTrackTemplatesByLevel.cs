using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KiteFlow.Services.Academics.Api.Migrations
{
    /// <inheritdoc />
    public partial class CourseTrackTemplatesByLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_courses_SchoolId_Level",
                table: "courses");

            migrationBuilder.DropIndex(
                name: "IX_course_level_settings_SchoolId_LevelValue",
                table: "course_level_settings");

            migrationBuilder.AddColumn<Guid>(
                name: "CourseLevelSettingId",
                table: "courses",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE courses AS c
                SET "CourseLevelSettingId" = cls."Id"
                FROM course_level_settings AS cls
                WHERE c."SchoolId" = cls."SchoolId"
                  AND c."Level" = cls."LevelValue"
                  AND c."CourseLevelSettingId" IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_courses_SchoolId_CourseLevelSettingId",
                table: "courses",
                columns: new[] { "SchoolId", "CourseLevelSettingId" });

            migrationBuilder.CreateIndex(
                name: "IX_courses_SchoolId_Level",
                table: "courses",
                columns: new[] { "SchoolId", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_course_level_settings_SchoolId_LevelValue",
                table: "course_level_settings",
                columns: new[] { "SchoolId", "LevelValue" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_courses_SchoolId_CourseLevelSettingId",
                table: "courses");

            migrationBuilder.DropIndex(
                name: "IX_courses_SchoolId_Level",
                table: "courses");

            migrationBuilder.DropIndex(
                name: "IX_course_level_settings_SchoolId_LevelValue",
                table: "course_level_settings");

            migrationBuilder.DropColumn(
                name: "CourseLevelSettingId",
                table: "courses");

            migrationBuilder.CreateIndex(
                name: "IX_courses_SchoolId_Level",
                table: "courses",
                columns: new[] { "SchoolId", "Level" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_course_level_settings_SchoolId_LevelValue",
                table: "course_level_settings",
                columns: new[] { "SchoolId", "LevelValue" },
                unique: true);
        }
    }
}
