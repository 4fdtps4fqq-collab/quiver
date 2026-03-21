using KiteFlow.Services.Schools.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Schools.Api.Data;

public sealed class SchoolsDbContext : DbContext
{
    public SchoolsDbContext(DbContextOptions<SchoolsDbContext> options)
        : base(options)
    {
    }

    public DbSet<School> Schools => Set<School>();

    public DbSet<SchoolSettings> SchoolSettings => Set<SchoolSettings>();

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<School>(entity =>
        {
            entity.ToTable("schools");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.LegalName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Cnpj).HasMaxLength(18);
            entity.Property(x => x.BaseBeachName).HasMaxLength(160);
            entity.Property(x => x.BaseLatitude);
            entity.Property(x => x.BaseLongitude);
            entity.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Timezone).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(x => x.LogoDataUrl);
            entity.Property(x => x.PostalCode).HasMaxLength(9);
            entity.Property(x => x.Street).HasMaxLength(200);
            entity.Property(x => x.StreetNumber).HasMaxLength(20);
            entity.Property(x => x.AddressComplement).HasMaxLength(120);
            entity.Property(x => x.Neighborhood).HasMaxLength(120);
            entity.Property(x => x.City).HasMaxLength(120);
            entity.Property(x => x.State).HasMaxLength(2);
            entity.Property(x => x.Status).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();

            entity.HasIndex(x => x.Slug).IsUnique();
        });

        modelBuilder.Entity<SchoolSettings>(entity =>
        {
            entity.ToTable("school_settings");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.BookingLeadTimeMinutes).IsRequired();
            entity.Property(x => x.CancellationWindowHours).IsRequired();
            entity.Property(x => x.RescheduleWindowHours).IsRequired();
            entity.Property(x => x.AttendanceConfirmationLeadMinutes).IsRequired();
            entity.Property(x => x.LessonReminderLeadHours).IsRequired();
            entity.Property(x => x.PortalNotificationsEnabled).IsRequired();
            entity.Property(x => x.InstructorBufferMinutes).IsRequired();
            entity.Property(x => x.NoShowGraceMinutes).IsRequired();
            entity.Property(x => x.NoShowConsumesCourseMinutes).IsRequired();
            entity.Property(x => x.NoShowChargesSingleLesson).IsRequired();
            entity.Property(x => x.AutoCreateEnrollmentRevenue).IsRequired();
            entity.Property(x => x.AutoCreateSingleLessonRevenue).IsRequired();
            entity.Property(x => x.ThemePrimary).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ThemeAccent).HasMaxLength(20).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();

            entity.HasOne(x => x.School)
                .WithMany()
                .HasForeignKey(x => x.SchoolId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.SchoolId).IsUnique();
        });

        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.ToTable("user_profiles");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Cpf).HasMaxLength(14);
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.SalaryAmount).HasColumnType("numeric(12,2)");
            entity.Property(x => x.PostalCode).HasMaxLength(9);
            entity.Property(x => x.Street).HasMaxLength(200);
            entity.Property(x => x.StreetNumber).HasMaxLength(20);
            entity.Property(x => x.AddressComplement).HasMaxLength(120);
            entity.Property(x => x.Neighborhood).HasMaxLength(120);
            entity.Property(x => x.City).HasMaxLength(120);
            entity.Property(x => x.State).HasMaxLength(2);
            entity.Property(x => x.AvatarUrl).HasMaxLength(500);
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();

            entity.HasIndex(x => new { x.SchoolId, x.FullName });
            entity.HasIndex(x => new { x.SchoolId, x.IdentityUserId }).IsUnique();
        });
    }
}
