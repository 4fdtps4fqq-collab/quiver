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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExpenseEntry>(entity =>
        {
            entity.ToTable("expense_entries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Category).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(12, 2).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Vendor).HasMaxLength(200);
            entity.Property(x => x.OccurredAtUtc).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.SchoolId, x.OccurredAtUtc });
        });

        modelBuilder.Entity<RevenueEntry>(entity =>
        {
            entity.ToTable("revenue_entries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SourceType).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(12, 2).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.Property(x => x.RecognizedAtUtc).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.SchoolId, x.RecognizedAtUtc });
            entity.HasIndex(x => new { x.SchoolId, x.SourceType, x.SourceId });
        });

        modelBuilder.Entity<AccountsReceivableEntry>(entity =>
        {
            entity.ToTable("accounts_receivable_entries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.StudentId).IsRequired();
            entity.Property(x => x.StudentNameSnapshot).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.Amount).HasPrecision(12, 2).IsRequired();
            entity.Property(x => x.PaidAmount).HasPrecision(12, 2).IsRequired();
            entity.Property(x => x.DueAtUtc).IsRequired();
            entity.Property(x => x.LastPaymentAtUtc);
            entity.Property(x => x.Status).IsRequired();
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
    }
}
