using KiteFlow.Services.Reporting.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Reporting.Api.Data;

public sealed class ReportingDbContext : DbContext
{
    public ReportingDbContext(DbContextOptions<ReportingDbContext> options)
        : base(options)
    {
    }

    public DbSet<ReportSnapshot> ReportSnapshots => Set<ReportSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReportSnapshot>(entity =>
        {
            entity.ToTable("report_snapshots");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ReportName).HasMaxLength(80).IsRequired();
            entity.Property(x => x.PayloadJson).HasColumnType("text").IsRequired();
            entity.Property(x => x.WindowStartUtc).IsRequired();
            entity.Property(x => x.WindowEndUtc).IsRequired();
            entity.Property(x => x.SnapshotVersion).IsRequired();
            entity.Property(x => x.GeneratedAtUtc).IsRequired();
            entity.Property(x => x.ExpiresAtUtc).IsRequired();
            entity.HasIndex(x => new { x.SchoolId, x.ReportName, x.WindowStartUtc, x.WindowEndUtc }).IsUnique();
            entity.HasIndex(x => new { x.SchoolId, x.ExpiresAtUtc });
        });
    }
}
