using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KiteFlow.Services.Reporting.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialReportingReadModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "report_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    WindowStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WindowEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SnapshotVersion = table.Column<int>(type: "integer", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_snapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_report_snapshots_SchoolId_ExpiresAtUtc",
                table: "report_snapshots",
                columns: new[] { "SchoolId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_report_snapshots_SchoolId_ReportName_WindowStartUtc_WindowE~",
                table: "report_snapshots",
                columns: new[] { "SchoolId", "ReportName", "WindowStartUtc", "WindowEndUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "report_snapshots");
        }
    }
}
