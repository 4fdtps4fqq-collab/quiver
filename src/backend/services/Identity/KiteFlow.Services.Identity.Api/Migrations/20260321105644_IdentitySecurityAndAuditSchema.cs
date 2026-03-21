using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KiteFlow.Services.Identity.Api.Migrations
{
    /// <inheritdoc />
    public partial class IdentitySecurityAndAuditSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
ALTER TABLE user_accounts
ADD COLUMN IF NOT EXISTS "PermissionsJson" text NULL;

ALTER TABLE refresh_sessions
ADD COLUMN IF NOT EXISTS "LastSeenAtUtc" timestamp with time zone NULL;

UPDATE refresh_sessions
SET "LastSeenAtUtc" = COALESCE("LastSeenAtUtc", "CreatedAtUtc");

ALTER TABLE refresh_sessions
ALTER COLUMN "LastSeenAtUtc" SET NOT NULL;
""");

            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS password_reset_tokens (
    "Id" uuid PRIMARY KEY,
    "UserAccountId" uuid NOT NULL REFERENCES user_accounts("Id") ON DELETE CASCADE,
    "TokenHash" character varying(128) NOT NULL,
    "ExpiresAtUtc" timestamp with time zone NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UsedAtUtc" timestamp with time zone NULL,
    "RequestedIpAddress" character varying(120) NULL,
    "RequestedUserAgent" character varying(500) NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_password_reset_tokens_TokenHash" ON password_reset_tokens ("TokenHash");
CREATE INDEX IF NOT EXISTS "IX_password_reset_tokens_UserAccountId_ExpiresAtUtc" ON password_reset_tokens ("UserAccountId", "ExpiresAtUtc");
""");

            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS authentication_audit_events (
    "Id" uuid PRIMARY KEY,
    "SchoolId" uuid NULL,
    "UserAccountId" uuid NULL,
    "TargetUserAccountId" uuid NULL,
    "EventType" character varying(80) NOT NULL,
    "Outcome" character varying(40) NOT NULL,
    "Email" character varying(320) NULL,
    "IpAddress" character varying(120) NULL,
    "UserAgent" character varying(500) NULL,
    "MetadataJson" text NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_authentication_audit_events_SchoolId_CreatedAtUtc" ON authentication_audit_events ("SchoolId", "CreatedAtUtc");
CREATE INDEX IF NOT EXISTS "IX_authentication_audit_events_UserAccountId_CreatedAtUtc" ON authentication_audit_events ("UserAccountId", "CreatedAtUtc");
CREATE INDEX IF NOT EXISTS "IX_authentication_audit_events_TargetUserAccountId_CreatedAtUtc" ON authentication_audit_events ("TargetUserAccountId", "CreatedAtUtc");
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intencionalmente vazio para evitar remoções destrutivas em bancos já sincronizados.
        }
    }
}
