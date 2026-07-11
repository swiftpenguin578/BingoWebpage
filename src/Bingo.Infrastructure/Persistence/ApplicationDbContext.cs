using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<SystemMetadata> SystemMetadata => Set<SystemMetadata>();

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
    }
}
