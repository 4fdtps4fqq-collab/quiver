using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KiteFlow.Services.Finance.Api.Migrations
{
    public partial class FinanceReceivablesAndStudentStatuses : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS accounts_receivable_entries
(
    "Id" uuid NOT NULL,
    "SchoolId" uuid NOT NULL,
    "StudentId" uuid NOT NULL,
    "EnrollmentId" uuid NULL,
    "StudentNameSnapshot" character varying(200) NOT NULL,
    "Description" character varying(500) NOT NULL,
    "Notes" character varying(1000) NULL,
    "Amount" numeric(12,2) NOT NULL,
    "PaidAmount" numeric(12,2) NOT NULL DEFAULT 0,
    "DueAtUtc" timestamp with time zone NOT NULL,
    "LastPaymentAtUtc" timestamp with time zone NULL,
    "Status" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_accounts_receivable_entries" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_accounts_receivable_entries_SchoolId_StudentId_DueAtUtc"
ON accounts_receivable_entries ("SchoolId", "StudentId", "DueAtUtc");

CREATE INDEX IF NOT EXISTS "IX_accounts_receivable_entries_SchoolId_Status_DueAtUtc"
ON accounts_receivable_entries ("SchoolId", "Status", "DueAtUtc");
""");

            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS accounts_receivable_payments
(
    "Id" uuid NOT NULL,
    "SchoolId" uuid NOT NULL,
    "ReceivableId" uuid NOT NULL,
    "Amount" numeric(12,2) NOT NULL,
    "PaidAtUtc" timestamp with time zone NOT NULL,
    "Note" character varying(500) NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_accounts_receivable_payments" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_accounts_receivable_payments_SchoolId_ReceivableId_PaidAtUtc"
ON accounts_receivable_payments ("SchoolId", "ReceivableId", "PaidAtUtc");
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intencionalmente vazio para evitar remoções destrutivas em bancos já sincronizados.
        }
    }
}
