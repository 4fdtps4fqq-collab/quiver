using KiteFlow.Services.Identity.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Identity.Api.Data;

public sealed class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();

    public DbSet<UserInvitation> UserInvitations => Set<UserInvitation>();

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public DbSet<AuthenticationAuditEvent> AuthenticationAuditEvents => Set<AuthenticationAuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("user_accounts");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
            entity.Property(x => x.PasswordHash).IsRequired();
            entity.Property(x => x.Role).IsRequired();
            entity.Property(x => x.PermissionsJson);
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.MustChangePassword).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();

            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => new { x.SchoolId, x.Role });
        });

        modelBuilder.Entity<RefreshSession>(entity =>
        {
            entity.ToTable("refresh_sessions");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.TokenHash).HasMaxLength(500).IsRequired();
            entity.Property(x => x.DeviceName).HasMaxLength(150);
            entity.Property(x => x.ExpiresAtUtc).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();

            entity.HasOne(x => x.UserAccount)
                .WithMany()
                .HasForeignKey(x => x.UserAccountId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.UserAccountId, x.ExpiresAtUtc });
        });

        modelBuilder.Entity<UserInvitation>(entity =>
        {
            entity.ToTable("user_invitations");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
            entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.Role).IsRequired();
            entity.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ExpiresAtUtc).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();

            entity.HasIndex(x => new { x.SchoolId, x.Email, x.CreatedAtUtc });
            entity.HasIndex(x => x.TokenHash).IsUnique();
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.ToTable("password_reset_tokens");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.RequestedIpAddress).HasMaxLength(120);
            entity.Property(x => x.RequestedUserAgent).HasMaxLength(500);
            entity.Property(x => x.ExpiresAtUtc).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();

            entity.HasOne(x => x.UserAccount)
                .WithMany()
                .HasForeignKey(x => x.UserAccountId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.UserAccountId, x.ExpiresAtUtc });
        });

        modelBuilder.Entity<AuthenticationAuditEvent>(entity =>
        {
            entity.ToTable("authentication_audit_events");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.EventType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Outcome).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.Property(x => x.IpAddress).HasMaxLength(120);
            entity.Property(x => x.UserAgent).HasMaxLength(500);
            entity.Property(x => x.MetadataJson);
            entity.Property(x => x.CreatedAtUtc).IsRequired();

            entity.HasIndex(x => new { x.SchoolId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.UserAccountId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.TargetUserAccountId, x.CreatedAtUtc });
        });
    }
}
