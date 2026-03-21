using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KiteFlow.Services.Schools.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfileSalaryAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "SalaryAmount",
                table: "user_profiles",
                type: "numeric(12,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SalaryAmount",
                table: "user_profiles");
        }
    }
}
