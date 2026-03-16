using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KiteFlow.Services.Schools.Api.Migrations;

[DbContext(typeof(Data.SchoolsDbContext))]
[Migration("20260314233000_SchoolsSaasMetadataAndAddress")]
public partial class SchoolsSaasMetadataAndAddress : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "LogoDataUrl",
            table: "schools",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Cnpj",
            table: "schools",
            type: "character varying(18)",
            maxLength: 18,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "BaseBeachName",
            table: "schools",
            type: "character varying(160)",
            maxLength: 160,
            nullable: true);

        migrationBuilder.AddColumn<double>(
            name: "BaseLatitude",
            table: "schools",
            type: "double precision",
            nullable: true);

        migrationBuilder.AddColumn<double>(
            name: "BaseLongitude",
            table: "schools",
            type: "double precision",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PostalCode",
            table: "schools",
            type: "character varying(9)",
            maxLength: 9,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Street",
            table: "schools",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "StreetNumber",
            table: "schools",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AddressComplement",
            table: "schools",
            type: "character varying(120)",
            maxLength: 120,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Neighborhood",
            table: "schools",
            type: "character varying(120)",
            maxLength: 120,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "City",
            table: "schools",
            type: "character varying(120)",
            maxLength: 120,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "State",
            table: "schools",
            type: "character varying(2)",
            maxLength: 2,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "RescheduleWindowHours",
            table: "school_settings",
            type: "integer",
            nullable: false,
            defaultValue: 24);

        migrationBuilder.AddColumn<int>(
            name: "AttendanceConfirmationLeadMinutes",
            table: "school_settings",
            type: "integer",
            nullable: false,
            defaultValue: 180);

        migrationBuilder.AddColumn<int>(
            name: "LessonReminderLeadHours",
            table: "school_settings",
            type: "integer",
            nullable: false,
            defaultValue: 18);

        migrationBuilder.AddColumn<bool>(
            name: "PortalNotificationsEnabled",
            table: "school_settings",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<string>(
            name: "Cpf",
            table: "user_profiles",
            type: "character varying(14)",
            maxLength: 14,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PostalCode",
            table: "user_profiles",
            type: "character varying(9)",
            maxLength: 9,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Street",
            table: "user_profiles",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "StreetNumber",
            table: "user_profiles",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AddressComplement",
            table: "user_profiles",
            type: "character varying(120)",
            maxLength: 120,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Neighborhood",
            table: "user_profiles",
            type: "character varying(120)",
            maxLength: 120,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "City",
            table: "user_profiles",
            type: "character varying(120)",
            maxLength: 120,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "State",
            table: "user_profiles",
            type: "character varying(2)",
            maxLength: 2,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "LogoDataUrl", table: "schools");
        migrationBuilder.DropColumn(name: "Cnpj", table: "schools");
        migrationBuilder.DropColumn(name: "BaseBeachName", table: "schools");
        migrationBuilder.DropColumn(name: "BaseLatitude", table: "schools");
        migrationBuilder.DropColumn(name: "BaseLongitude", table: "schools");
        migrationBuilder.DropColumn(name: "PostalCode", table: "schools");
        migrationBuilder.DropColumn(name: "Street", table: "schools");
        migrationBuilder.DropColumn(name: "StreetNumber", table: "schools");
        migrationBuilder.DropColumn(name: "AddressComplement", table: "schools");
        migrationBuilder.DropColumn(name: "Neighborhood", table: "schools");
        migrationBuilder.DropColumn(name: "City", table: "schools");
        migrationBuilder.DropColumn(name: "State", table: "schools");

        migrationBuilder.DropColumn(name: "RescheduleWindowHours", table: "school_settings");
        migrationBuilder.DropColumn(name: "AttendanceConfirmationLeadMinutes", table: "school_settings");
        migrationBuilder.DropColumn(name: "LessonReminderLeadHours", table: "school_settings");
        migrationBuilder.DropColumn(name: "PortalNotificationsEnabled", table: "school_settings");

        migrationBuilder.DropColumn(name: "Cpf", table: "user_profiles");
        migrationBuilder.DropColumn(name: "PostalCode", table: "user_profiles");
        migrationBuilder.DropColumn(name: "Street", table: "user_profiles");
        migrationBuilder.DropColumn(name: "StreetNumber", table: "user_profiles");
        migrationBuilder.DropColumn(name: "AddressComplement", table: "user_profiles");
        migrationBuilder.DropColumn(name: "Neighborhood", table: "user_profiles");
        migrationBuilder.DropColumn(name: "City", table: "user_profiles");
        migrationBuilder.DropColumn(name: "State", table: "user_profiles");
    }
}
