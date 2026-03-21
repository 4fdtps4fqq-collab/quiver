using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KiteFlow.Services.Finance.Api.Migrations
{
    /// <inheritdoc />
    public partial class FinanceOperationalSchema : Migration
    {
        /// <inheritdoc />
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

            migrationBuilder.Sql("""
ALTER TABLE revenue_entries
    ADD COLUMN IF NOT EXISTS "CategoryId" uuid NULL,
    ADD COLUMN IF NOT EXISTS "CostCenterId" uuid NULL,
    ADD COLUMN IF NOT EXISTS "CostCenterName" character varying(120) NULL,
    ADD COLUMN IF NOT EXISTS "ReconciledAtUtc" timestamp with time zone NULL,
    ADD COLUMN IF NOT EXISTS "ReconciledByUserId" character varying(120) NULL,
    ADD COLUMN IF NOT EXISTS "ReconciliationNote" character varying(500) NULL;

ALTER TABLE expense_entries
    ADD COLUMN IF NOT EXISTS "CategoryId" uuid NULL,
    ADD COLUMN IF NOT EXISTS "CategoryName" character varying(120) NULL,
    ADD COLUMN IF NOT EXISTS "CostCenterId" uuid NULL,
    ADD COLUMN IF NOT EXISTS "CostCenterName" character varying(120) NULL,
    ADD COLUMN IF NOT EXISTS "ReconciledAtUtc" timestamp with time zone NULL,
    ADD COLUMN IF NOT EXISTS "ReconciledByUserId" character varying(120) NULL,
    ADD COLUMN IF NOT EXISTS "ReconciliationNote" character varying(500) NULL,
    ADD COLUMN IF NOT EXISTS "SourceId" uuid NULL,
    ADD COLUMN IF NOT EXISTS "SourceType" character varying(100) NULL;

ALTER TABLE accounts_receivable_entries
    ADD COLUMN IF NOT EXISTS "CategoryId" uuid NULL,
    ADD COLUMN IF NOT EXISTS "CategoryName" character varying(120) NULL,
    ADD COLUMN IF NOT EXISTS "CostCenterId" uuid NULL,
    ADD COLUMN IF NOT EXISTS "CostCenterName" character varying(120) NULL,
    ADD COLUMN IF NOT EXISTS "ReconciledAtUtc" timestamp with time zone NULL,
    ADD COLUMN IF NOT EXISTS "ReconciledByUserId" character varying(120) NULL,
    ADD COLUMN IF NOT EXISTS "ReconciliationNote" character varying(500) NULL;
""");

            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS accounts_payable_entries
(
    "Id" uuid NOT NULL,
    "SchoolId" uuid NOT NULL,
    "Description" character varying(500) NOT NULL,
    "Notes" character varying(1000) NULL,
    "Vendor" character varying(200) NULL,
    "SourceType" character varying(100) NULL,
    "SourceId" uuid NULL,
    "CategoryId" uuid NULL,
    "CategoryName" character varying(120) NULL,
    "CostCenterId" uuid NULL,
    "CostCenterName" character varying(120) NULL,
    "Amount" numeric(12,2) NOT NULL,
    "PaidAmount" numeric(12,2) NOT NULL DEFAULT 0,
    "DueAtUtc" timestamp with time zone NOT NULL,
    "LastPaymentAtUtc" timestamp with time zone NULL,
    "Status" integer NOT NULL,
    "ReconciledAtUtc" timestamp with time zone NULL,
    "ReconciledByUserId" character varying(120) NULL,
    "ReconciliationNote" character varying(500) NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_accounts_payable_entries" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_accounts_payable_entries_SchoolId_Status_DueAtUtc"
ON accounts_payable_entries ("SchoolId", "Status", "DueAtUtc");

CREATE INDEX IF NOT EXISTS "IX_accounts_payable_entries_SchoolId_SourceType_SourceId"
ON accounts_payable_entries ("SchoolId", "SourceType", "SourceId");
""");

            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS accounts_payable_payments
(
    "Id" uuid NOT NULL,
    "SchoolId" uuid NOT NULL,
    "PayableId" uuid NOT NULL,
    "Amount" numeric(12,2) NOT NULL,
    "PaidAtUtc" timestamp with time zone NOT NULL,
    "Note" character varying(500) NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_accounts_payable_payments" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_accounts_payable_payments_SchoolId_PayableId_PaidAtUtc"
ON accounts_payable_payments ("SchoolId", "PayableId", "PaidAtUtc");
""");

            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS financial_categories
(
    "Id" uuid NOT NULL,
    "SchoolId" uuid NOT NULL,
    "Name" character varying(120) NOT NULL,
    "Direction" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    "SortOrder" integer NOT NULL DEFAULT 0,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_financial_categories" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_financial_categories_SchoolId_Name"
ON financial_categories ("SchoolId", "Name");
""");

            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS cost_centers
(
    "Id" uuid NOT NULL,
    "SchoolId" uuid NOT NULL,
    "Name" character varying(120) NOT NULL,
    "Description" character varying(300) NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_cost_centers" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_cost_centers_SchoolId_Name"
ON cost_centers ("SchoolId", "Name");
""");

            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS financial_reconciliation_records
(
    "Id" uuid NOT NULL,
    "SchoolId" uuid NOT NULL,
    "EntryKind" integer NOT NULL,
    "EntryId" uuid NOT NULL,
    "AmountSnapshot" numeric(12,2) NOT NULL,
    "ReconciledAtUtc" timestamp with time zone NOT NULL,
    "ReconciledByUserId" character varying(120) NOT NULL,
    "Note" character varying(500) NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_financial_reconciliation_records" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_financial_reconciliation_records_SchoolId_EntryKind_EntryId_ReconciledAtUtc"
ON financial_reconciliation_records ("SchoolId", "EntryKind", "EntryId", "ReconciledAtUtc");
""");

            migrationBuilder.Sql("""
DROP INDEX IF EXISTS "IX_revenue_entries_SchoolId_SourceType_SourceId";
CREATE UNIQUE INDEX IF NOT EXISTS "IX_revenue_entries_SchoolId_SourceType_SourceId"
ON revenue_entries ("SchoolId", "SourceType", "SourceId")
WHERE "SourceId" <> '00000000-0000-0000-0000-000000000000';

CREATE INDEX IF NOT EXISTS "IX_expense_entries_SchoolId_SourceType_SourceId"
ON expense_entries ("SchoolId", "SourceType", "SourceId");
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intencionalmente vazio para evitar remoções destrutivas em bancos já sincronizados.
        }
    }
}
