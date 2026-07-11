using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<SystemMetadata> SystemMetadata => Set<SystemMetadata>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SystemMetadata>(entity =>
        {
            entity.ToTable("system_metadata");
            entity.HasKey(item => item.Key);
            entity.Property(item => item.Key).HasColumnName("key").HasMaxLength(100);
            entity.Property(item => item.Value).HasColumnName("value").HasMaxLength(1_000);
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("accounts");
            entity.HasKey(account => account.Id);
            entity.Property(account => account.Id).HasColumnName("id");
            entity.Property(account => account.Username).HasColumnName("username").HasMaxLength(100);
            entity.Property(account => account.NormalizedUsername).HasColumnName("normalized_username").HasMaxLength(100);
            entity.HasIndex(account => account.NormalizedUsername).IsUnique();
            entity.Property(account => account.PasswordHash).HasColumnName("password_hash").HasMaxLength(1_000);
            entity.Property(account => account.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20);
            entity.Property(account => account.EventId).HasColumnName("event_id");
            entity.Property(account => account.TeamId).HasColumnName("team_id");
            entity.Property(account => account.ActiveFrom).HasColumnName("active_from");
            entity.Property(account => account.CorrectionOnlyFrom).HasColumnName("correction_only_from");
            entity.Property(account => account.ExpiresAt).HasColumnName("expires_at");
            entity.Property(account => account.DisabledAt).HasColumnName("disabled_at");
            entity.Property(account => account.CreatedAt).HasColumnName("created_at");
            entity.Property(account => account.LastLoginAt).HasColumnName("last_login_at");
            entity.Property(account => account.MustChangePassword).HasColumnName("must_change_password");
        });

        modelBuilder.Entity<AuditEntry>(entity =>
        {
            entity.ToTable("audit_entries");
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.Id).HasColumnName("id");
            entity.Property(entry => entry.OccurredAt).HasColumnName("occurred_at");
            entity.Property(entry => entry.ActorAccountId).HasColumnName("actor_account_id");
            entity.Property(entry => entry.ActorUsername).HasColumnName("actor_username").HasMaxLength(100);
            entity.Property(entry => entry.Action).HasColumnName("action").HasMaxLength(100);
            entity.Property(entry => entry.TargetType).HasColumnName("target_type").HasMaxLength(100);
            entity.Property(entry => entry.TargetId).HasColumnName("target_id").HasMaxLength(100);
            entity.Property(entry => entry.Details).HasColumnName("details").HasMaxLength(4_000);
            entity.HasIndex(entry => entry.OccurredAt);
            entity.HasIndex(entry => new { entry.Action, entry.TargetType });
        });
    }
}
