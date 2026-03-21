using KiteFlow.Services.Finance.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Finance.Api.Data;

public sealed class FinanceDbContext : DbContext
{
    public FinanceDbContext(DbContextOptions<FinanceDbContext> options)
        : base(options)
    {
    }

    public DbSet<ExpenseEntry> ExpenseEntries => Set<ExpenseEntry>();

    public DbSet<RevenueEntry> RevenueEntries => Set<RevenueEntry>();

    public DbSet<AccountsReceivableEntry> AccountsReceivableEntries => Set<AccountsReceivableEntry>();

    public DbSet<AccountsReceivablePayment> AccountsReceivablePayments => Set<AccountsReceivablePayment>();

    public DbSet<AccountsPayableEntry> AccountsPayableEntries => Set<AccountsPayableEntry>();

    public DbSet<AccountsPayablePayment> AccountsPayablePayments => Set<AccountsPayablePayment>();

    public DbSet<FinancialCategory> FinancialCategories => Set<FinancialCategory>();

    public DbSet<CostCenter> CostCenters => Set<CostCenter>();

    public DbSet<FinancialReconciliationRecord> FinancialReconciliationRecords => Set<FinancialReconciliationRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExpenseEntry>(entity =>
        {
            entity.ToTable("expense_entries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SourceType).HasMaxLength(100);
            entity.Property(x => x.Category).IsRequired();
            entity.Property(x => x.CategoryName).HasMaxLength(120);
            entity.Property(x => x.CostCenterName).HasMaxLength(120);
            entity.Property(x => x.Amount).HasPrecision(12, 2).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Vendor).HasMaxLength(200);
            entity.Property(x => x.OccurredAtUtc).IsRequired();
            entity.Property(x => x.ReconciledByUserId).HasMaxLength(120);
            entity.Property(x => x.ReconciliationNote).HasMaxLength(500);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.SchoolId, x.OccurredAtUtc });
            entity.HasIndex(x => new { x.SchoolId, x.SourceType, x.SourceId });
        });

        modelBuilder.Entity<RevenueEntry>(entity =>
        {
            entity.ToTable("revenue_entries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SourceType).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CostCenterName).HasMaxLength(120);
            entity.Property(x => x.Amount).HasPrecision(12, 2).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.Property(x => x.RecognizedAtUtc).IsRequired();
            entity.Property(x => x.ReconciledByUserId).HasMaxLength(120);
            entity.Property(x => x.ReconciliationNote).HasMaxLength(500);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.SchoolId, x.RecognizedAtUtc });
            entity.HasIndex(x => new { x.SchoolId, x.SourceType, x.SourceId })
                .IsUnique()
                .HasFilter("\"SourceId\" <> '00000000-0000-0000-0000-000000000000'");
        });

        modelBuilder.Entity<AccountsReceivableEntry>(entity =>
        {
            entity.ToTable("accounts_receivable_entries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.StudentId).IsRequired();
            entity.Property(x => x.StudentNameSnapshot).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.CategoryName).HasMaxLength(120);
            entity.Property(x => x.CostCenterName).HasMaxLength(120);
            entity.Property(x => x.Amount).HasPrecision(12, 2).IsRequired();
            entity.Property(x => x.PaidAmount).HasPrecision(12, 2).IsRequired();
            entity.Property(x => x.DueAtUtc).IsRequired();
            entity.Property(x => x.LastPaymentAtUtc);
            entity.Property(x => x.Status).IsRequired();
            entity.Property(x => x.ReconciledByUserId).HasMaxLength(120);
            entity.Property(x => x.ReconciliationNote).HasMaxLength(500);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.SchoolId, x.StudentId, x.DueAtUtc });
            entity.HasIndex(x => new { x.SchoolId, x.Status, x.DueAtUtc });
        });

        modelBuilder.Entity<AccountsReceivablePayment>(entity =>
        {
            entity.ToTable("accounts_receivable_payments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ReceivableId).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(12, 2).IsRequired();
            entity.Property(x => x.PaidAtUtc).IsRequired();
            entity.Property(x => x.Note).HasMaxLength(500);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.SchoolId, x.ReceivableId, x.PaidAtUtc });
        });

        modelBuilder.Entity<AccountsPayableEntry>(entity =>
        {
            entity.ToTable("accounts_payable_entries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.Vendor).HasMaxLength(200);
            entity.Property(x => x.SourceType).HasMaxLength(100);
            entity.Property(x => x.CategoryName).HasMaxLength(120);
            entity.Property(x => x.CostCenterName).HasMaxLength(120);
            entity.Property(x => x.Amount).HasPrecision(12, 2).IsRequired();
            entity.Property(x => x.PaidAmount).HasPrecision(12, 2).IsRequired();
            entity.Property(x => x.DueAtUtc).IsRequired();
            entity.Property(x => x.Status).IsRequired();
            entity.Property(x => x.ReconciledByUserId).HasMaxLength(120);
            entity.Property(x => x.ReconciliationNote).HasMaxLength(500);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.SchoolId, x.Status, x.DueAtUtc });
            entity.HasIndex(x => new { x.SchoolId, x.SourceType, x.SourceId });
        });

        modelBuilder.Entity<AccountsPayablePayment>(entity =>
        {
            entity.ToTable("accounts_payable_payments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PayableId).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(12, 2).IsRequired();
            entity.Property(x => x.PaidAtUtc).IsRequired();
            entity.Property(x => x.Note).HasMaxLength(500);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.SchoolId, x.PayableId, x.PaidAtUtc });
        });

        modelBuilder.Entity<FinancialCategory>(entity =>
        {
            entity.ToTable("financial_categories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Direction).IsRequired();
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.SortOrder).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.SchoolId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<CostCenter>(entity =>
        {
            entity.ToTable("cost_centers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(300);
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.SchoolId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<FinancialReconciliationRecord>(entity =>
        {
            entity.ToTable("financial_reconciliation_records");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EntryKind).IsRequired();
            entity.Property(x => x.EntryId).IsRequired();
            entity.Property(x => x.AmountSnapshot).HasPrecision(12, 2).IsRequired();
            entity.Property(x => x.ReconciledAtUtc).IsRequired();
            entity.Property(x => x.ReconciledByUserId).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Note).HasMaxLength(500);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.SchoolId, x.EntryKind, x.EntryId, x.ReconciledAtUtc });
        });
    }
}
