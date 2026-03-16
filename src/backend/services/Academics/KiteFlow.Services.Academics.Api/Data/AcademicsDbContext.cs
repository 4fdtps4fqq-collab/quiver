using KiteFlow.Services.Academics.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Academics.Api.Data;

public sealed class AcademicsDbContext : DbContext
{
    public AcademicsDbContext(DbContextOptions<AcademicsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Student> Students => Set<Student>();

    public DbSet<Instructor> Instructors => Set<Instructor>();

    public DbSet<Course> Courses => Set<Course>();

    public DbSet<Enrollment> Enrollments => Set<Enrollment>();

    public DbSet<EnrollmentBalanceLedgerEntry> EnrollmentBalanceLedger => Set<EnrollmentBalanceLedgerEntry>();

    public DbSet<Lesson> Lessons => Set<Lesson>();

    public DbSet<ScheduleBlock> ScheduleBlocks => Set<ScheduleBlock>();

    public DbSet<StudentPortalNotification> StudentPortalNotifications => Set<StudentPortalNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Student>(entity =>
        {
            entity.ToTable("students");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.PostalCode).HasMaxLength(20);
            entity.Property(x => x.Street).HasMaxLength(200);
            entity.Property(x => x.StreetNumber).HasMaxLength(30);
            entity.Property(x => x.AddressComplement).HasMaxLength(120);
            entity.Property(x => x.Neighborhood).HasMaxLength(120);
            entity.Property(x => x.City).HasMaxLength(120);
            entity.Property(x => x.State).HasMaxLength(80);
            entity.Property(x => x.MedicalNotes).HasMaxLength(2000);
            entity.Property(x => x.EmergencyContactName).HasMaxLength(200);
            entity.Property(x => x.EmergencyContactPhone).HasMaxLength(50);
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.SchoolId, x.FullName });
            entity.HasIndex(x => new { x.SchoolId, x.IdentityUserId })
                .IsUnique()
                .HasFilter("\"IdentityUserId\" IS NOT NULL");
        });

        modelBuilder.Entity<Instructor>(entity =>
        {
            entity.ToTable("instructors");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.Specialties).HasMaxLength(500);
            entity.Property(x => x.AvailabilityJson).HasMaxLength(4000);
            entity.Property(x => x.HourlyRate).HasPrecision(12, 2).IsRequired();
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.SchoolId, x.FullName });
            entity.HasIndex(x => new { x.SchoolId, x.IdentityUserId }).IsUnique(false);
        });

        modelBuilder.Entity<Course>(entity =>
        {
            entity.ToTable("courses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Level).IsRequired();
            entity.Property(x => x.TotalMinutes).IsRequired();
            entity.Property(x => x.Price).HasPrecision(12, 2).IsRequired();
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.SchoolId, x.Level }).IsUnique();
        });

        modelBuilder.Entity<Enrollment>(entity =>
        {
            entity.ToTable("enrollments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).IsRequired();
            entity.Property(x => x.IncludedMinutesSnapshot).IsRequired();
            entity.Property(x => x.UsedMinutes).IsRequired();
            entity.Property(x => x.CoursePriceSnapshot).HasPrecision(12, 2).IsRequired();
            entity.Property(x => x.StartedAtUtc).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();

            entity.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Course)
                .WithMany()
                .HasForeignKey(x => x.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.SchoolId, x.StudentId, x.Status });
        });

        modelBuilder.Entity<EnrollmentBalanceLedgerEntry>(entity =>
        {
            entity.ToTable("enrollment_balance_ledger");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DeltaMinutes).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(120).IsRequired();
            entity.Property(x => x.OccurredAtUtc).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();

            entity.HasOne(x => x.Enrollment)
                .WithMany()
                .HasForeignKey(x => x.EnrollmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.SchoolId, x.EnrollmentId, x.OccurredAtUtc });
        });

        modelBuilder.Entity<Lesson>(entity =>
        {
            entity.ToTable("lessons");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Kind).IsRequired();
            entity.Property(x => x.Status).IsRequired();
            entity.Property(x => x.SingleLessonPrice).HasPrecision(12, 2);
            entity.Property(x => x.StartAtUtc).IsRequired();
            entity.Property(x => x.DurationMinutes).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.OperationalConfirmationNote).HasMaxLength(1000);
            entity.Property(x => x.StudentConfirmationNote).HasMaxLength(1000);
            entity.Property(x => x.NoShowNote).HasMaxLength(1000);
            entity.Property(x => x.CreatedAtUtc).IsRequired();

            entity.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Instructor)
                .WithMany()
                .HasForeignKey(x => x.InstructorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Enrollment)
                .WithMany()
                .HasForeignKey(x => x.EnrollmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.SchoolId, x.StartAtUtc });
            entity.HasIndex(x => new { x.SchoolId, x.InstructorId, x.StartAtUtc });
            entity.HasIndex(x => new { x.SchoolId, x.StudentId, x.StartAtUtc });
        });

        modelBuilder.Entity<ScheduleBlock>(entity =>
        {
            entity.ToTable("schedule_blocks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Scope).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.StartAtUtc).IsRequired();
            entity.Property(x => x.EndAtUtc).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();

            entity.HasOne(x => x.Instructor)
                .WithMany()
                .HasForeignKey(x => x.InstructorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.SchoolId, x.StartAtUtc, x.EndAtUtc });
            entity.HasIndex(x => new { x.SchoolId, x.InstructorId, x.StartAtUtc, x.EndAtUtc });
        });

        modelBuilder.Entity<StudentPortalNotification>(entity =>
        {
            entity.ToTable("student_portal_notifications");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Category).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(1500).IsRequired();
            entity.Property(x => x.ActionLabel).HasMaxLength(80);
            entity.Property(x => x.ActionPath).HasMaxLength(250);
            entity.Property(x => x.CreatedAtUtc).IsRequired();

            entity.HasIndex(x => new { x.SchoolId, x.StudentId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.SchoolId, x.StudentId, x.ReadAtUtc });
        });
    }
}
