using KiteFlow.Services.Equipment.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Equipment.Api.Data;

public sealed class EquipmentDbContext : DbContext
{
    public EquipmentDbContext(DbContextOptions<EquipmentDbContext> options)
        : base(options)
    {
    }

    public DbSet<GearStorage> GearStorages => Set<GearStorage>();

    public DbSet<EquipmentItem> EquipmentItems => Set<EquipmentItem>();

    public DbSet<LessonEquipmentCheckout> LessonEquipmentCheckouts => Set<LessonEquipmentCheckout>();

    public DbSet<LessonEquipmentCheckoutItem> LessonEquipmentCheckoutItems => Set<LessonEquipmentCheckoutItem>();

    public DbSet<EquipmentUsageLog> EquipmentUsageLogs => Set<EquipmentUsageLog>();

    public DbSet<EquipmentReservation> EquipmentReservations => Set<EquipmentReservation>();

    public DbSet<EquipmentReservationItem> EquipmentReservationItems => Set<EquipmentReservationItem>();

    public DbSet<EquipmentKit> EquipmentKits => Set<EquipmentKit>();

    public DbSet<EquipmentKitItem> EquipmentKitItems => Set<EquipmentKitItem>();

    public DbSet<MaintenanceRule> MaintenanceRules => Set<MaintenanceRule>();

    public DbSet<MaintenanceRecord> MaintenanceRecords => Set<MaintenanceRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GearStorage>(entity =>
        {
            entity.ToTable("gear_storages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.LocationNote).HasMaxLength(300);
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.SchoolId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<EquipmentItem>(entity =>
        {
            entity.ToTable("equipment_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(250).IsRequired();
            entity.Property(x => x.TagCode).HasMaxLength(100);
            entity.Property(x => x.Brand).HasMaxLength(120);
            entity.Property(x => x.Model).HasMaxLength(120);
            entity.Property(x => x.SizeLabel).HasMaxLength(50);
            entity.Property(x => x.Category).HasMaxLength(120);
            entity.Property(x => x.Type).IsRequired();
            entity.Property(x => x.CurrentCondition).IsRequired();
            entity.Property(x => x.OwnershipType).IsRequired();
            entity.Property(x => x.OwnerDisplayName).HasMaxLength(200);
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();

            entity.HasOne(x => x.Storage)
                .WithMany()
                .HasForeignKey(x => x.StorageId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.SchoolId, x.Type });
            entity.HasIndex(x => new { x.SchoolId, x.TagCode });
            entity.HasIndex(x => new { x.SchoolId, x.Category });
        });

        modelBuilder.Entity<LessonEquipmentCheckout>(entity =>
        {
            entity.ToTable("lesson_equipment_checkouts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CheckedOutAtUtc).IsRequired();
            entity.Property(x => x.NotesBefore).HasMaxLength(2000);
            entity.Property(x => x.NotesAfter).HasMaxLength(2000);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.SchoolId, x.LessonId }).IsUnique();
        });

        modelBuilder.Entity<LessonEquipmentCheckoutItem>(entity =>
        {
            entity.ToTable("lesson_equipment_checkout_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ConditionBefore).IsRequired();
            entity.Property(x => x.ConditionAfter);
            entity.Property(x => x.NotesBefore).HasMaxLength(1000);
            entity.Property(x => x.NotesAfter).HasMaxLength(1000);
            entity.Property(x => x.CreatedAtUtc).IsRequired();

            entity.HasOne(x => x.Checkout)
                .WithMany()
                .HasForeignKey(x => x.CheckoutId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Equipment)
                .WithMany()
                .HasForeignKey(x => x.EquipmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.SchoolId, x.CheckoutId, x.EquipmentId }).IsUnique();
        });

        modelBuilder.Entity<EquipmentUsageLog>(entity =>
        {
            entity.ToTable("equipment_usage_logs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UsageMinutes).IsRequired();
            entity.Property(x => x.ConditionAfter).IsRequired();
            entity.Property(x => x.RecordedAtUtc).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();

            entity.HasOne(x => x.Equipment)
                .WithMany()
                .HasForeignKey(x => x.EquipmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.SchoolId, x.EquipmentId, x.RecordedAtUtc });
        });

        modelBuilder.Entity<EquipmentReservation>(entity =>
        {
            entity.ToTable("equipment_reservations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ReservedFromUtc).IsRequired();
            entity.Property(x => x.ReservedUntilUtc).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.SchoolId, x.LessonId }).IsUnique();
            entity.HasIndex(x => new { x.SchoolId, x.ReservedFromUtc, x.ReservedUntilUtc });
        });

        modelBuilder.Entity<EquipmentReservationItem>(entity =>
        {
            entity.ToTable("equipment_reservation_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CreatedAtUtc).IsRequired();

            entity.HasOne(x => x.Reservation)
                .WithMany()
                .HasForeignKey(x => x.ReservationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Equipment)
                .WithMany()
                .HasForeignKey(x => x.EquipmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.SchoolId, x.ReservationId, x.EquipmentId }).IsUnique();
            entity.HasIndex(x => new { x.SchoolId, x.EquipmentId });
        });

        modelBuilder.Entity<EquipmentKit>(entity =>
        {
            entity.ToTable("equipment_kits");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.SchoolId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<EquipmentKitItem>(entity =>
        {
            entity.ToTable("equipment_kit_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CreatedAtUtc).IsRequired();

            entity.HasOne(x => x.Kit)
                .WithMany()
                .HasForeignKey(x => x.KitId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Equipment)
                .WithMany()
                .HasForeignKey(x => x.EquipmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.SchoolId, x.KitId, x.EquipmentId }).IsUnique();
        });

        modelBuilder.Entity<MaintenanceRule>(entity =>
        {
            entity.ToTable("maintenance_rules");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EquipmentType).IsRequired();
            entity.Property(x => x.PlanName).HasMaxLength(150).IsRequired();
            entity.Property(x => x.ServiceCategory).IsRequired();
            entity.Property(x => x.Checklist).HasMaxLength(2000);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.SchoolId, x.EquipmentType }).IsUnique();
        });

        modelBuilder.Entity<MaintenanceRecord>(entity =>
        {
            entity.ToTable("maintenance_records");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Description).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.PerformedBy).HasMaxLength(200);
            entity.Property(x => x.CounterpartyName).HasMaxLength(200);
            entity.Property(x => x.Cost).HasPrecision(12, 2);
            entity.Property(x => x.ServiceCategory).IsRequired();
            entity.Property(x => x.FinancialEffect).IsRequired();
            entity.Property(x => x.ServiceDateUtc).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();

            entity.HasOne(x => x.Equipment)
                .WithMany()
                .HasForeignKey(x => x.EquipmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.SchoolId, x.ServiceDateUtc });
        });
    }
}
