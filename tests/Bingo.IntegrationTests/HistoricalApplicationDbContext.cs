using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

/// <summary>
/// EF model for the predecessor schema used by retained-migration fixtures.
/// The production model is intentionally current; this model omits only columns
/// introduced after the predecessor migration so fixture setup cannot query or
/// write against columns that do not yet exist.
/// </summary>
internal sealed class HistoricalApplicationDbContext(DbContextOptions<HistoricalApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<BingoEvent> Events => Set<BingoEvent>();
    public DbSet<EventItemPrice> EventItemPrices => Set<EventItemPrice>();
    public DbSet<EventParticipant> EventParticipants => Set<EventParticipant>();
    public DbSet<EventParticipantCharacter> EventParticipantCharacters => Set<EventParticipantCharacter>();
    public DbSet<SignupForm> SignupForms => Set<SignupForm>();
    public DbSet<SignupQuestion> SignupQuestions => Set<SignupQuestion>();
    public DbSet<OsrsCharacter> OsrsCharacters => Set<OsrsCharacter>();
    public DbSet<BossActivity> BossActivities => Set<BossActivity>();
    public DbSet<CatalogueItem> CatalogueItems => Set<CatalogueItem>();
    public DbSet<SourceDrop> SourceDrops => Set<SourceDrop>();
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMembership> TeamMemberships => Set<TeamMembership>();
    public DbSet<DraftSession> DraftSessions => Set<DraftSession>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AdvanceVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AdvanceVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("accounts");
            entity.HasKey(account => account.Id);
            entity.Property(account => account.Id).HasColumnName("id");
            entity.Property(account => account.AccountType).HasColumnName("account_type").HasConversion<string>().HasMaxLength(30);
            entity.Property(account => account.GlobalRole).HasColumnName("global_role").HasConversion<string>().HasMaxLength(30);
            entity.Property(account => account.LoginName).HasColumnName("login_name").HasMaxLength(100);
            entity.Property(account => account.NormalizedLoginName).HasColumnName("normalized_login_name").HasMaxLength(100);
            entity.Property(account => account.PublicUsername).HasColumnName("public_username").HasMaxLength(100);
            entity.Property(account => account.NormalizedPublicUsername).HasColumnName("normalized_public_username").HasMaxLength(100);
            entity.Property(account => account.EmergencyLoginUsername).HasColumnName("emergency_login_username").HasMaxLength(100);
            entity.Property(account => account.DiscordUserId).HasColumnName("discord_user_id").HasMaxLength(100);
            entity.Property(account => account.DiscordDisplayName).HasColumnName("discord_display_name").HasMaxLength(200);
            entity.Property(account => account.PasswordHash).HasColumnName("password_hash").HasMaxLength(1_000);
            entity.Property(account => account.PasswordChangedAt).HasColumnName("password_changed_at");
            entity.Property(account => account.AuthorizationVersion).HasColumnName("authorization_version");
            entity.Property(account => account.PasswordVersion).HasColumnName("password_version");
            entity.Property(account => account.Active).HasColumnName("active");
            entity.Property(account => account.DisabledAt).HasColumnName("disabled_at");
            entity.Property(account => account.DisabledByAccountId).HasColumnName("disabled_by_account_id");
            entity.Property(account => account.DisabledReason).HasColumnName("disabled_reason").HasMaxLength(500);
            entity.Property(account => account.OnboardingCompletedAt).HasColumnName("onboarding_completed_at");
            entity.Property(account => account.ProfileOsrsCharacterId).HasColumnName("profile_osrs_character_id");
            entity.Property(account => account.CreatedAt).HasColumnName("created_at");
            entity.Property(account => account.LastLoginAt).HasColumnName("last_login_at");
            entity.Property(account => account.MustChangePassword).HasColumnName("must_change_password");
            entity.Property(account => account.StatsGuidanceHidden).HasColumnName("stats_guidance_hidden").HasDefaultValue(false);
            entity.Property(account => account.Version).HasColumnName("version").IsConcurrencyToken();
        });

        modelBuilder.Entity<OsrsCharacter>(entity =>
        {
            entity.ToTable("osrs_characters");
            entity.HasKey(character => character.Id);
            entity.Property(character => character.Id).HasColumnName("Id");
            entity.Property(character => character.DisplayName).HasColumnName("DisplayName").HasMaxLength(100);
            entity.Property(character => character.NormalizedName).HasColumnName("NormalizedName").HasMaxLength(100);
            entity.Property(character => character.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(character => character.UpdatedAt).HasColumnName("UpdatedAt");
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
            entity.Property(entry => entry.EventId).HasColumnName("event_id");
            entity.Property(entry => entry.BeforeState).HasColumnName("before_state").HasMaxLength(4_000);
            entity.Property(entry => entry.AfterState).HasColumnName("after_state").HasMaxLength(4_000);
        });

        modelBuilder.Entity<BossActivity>().Ignore(activity => activity.TeamSize);
        modelBuilder.Entity<BingoEvent>().Ignore(eventItem => eventItem.PlacementRule);
        modelBuilder.Entity<DraftSession>().Ignore(draft => draft.RequiresFreshOrder);
    }

    private void AdvanceVersions()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries<Account>().Where(entry => entry.State == EntityState.Modified)) entry.Entity.AdvanceVersion();
        foreach (var entry in ChangeTracker.Entries<EventParticipantCharacter>().Where(entry => entry.State == EntityState.Modified)) entry.Entity.AdvanceVersion();
        foreach (var entry in ChangeTracker.Entries<SignupForm>().Where(entry => entry.State == EntityState.Modified)) entry.Entity.AdvanceVersion();
        foreach (var entry in ChangeTracker.Entries<SignupQuestion>().Where(entry => entry.State == EntityState.Modified)) entry.Entity.AdvanceVersion();
        foreach (var entry in ChangeTracker.Entries<BingoEvent>().Where(entry => entry.State == EntityState.Modified)) entry.Entity.AdvanceVersion();
        foreach (var entry in ChangeTracker.Entries<Team>().Where(entry => entry.State == EntityState.Modified)) entry.Entity.AdvanceVersion();
        foreach (var entry in ChangeTracker.Entries<TeamMembership>().Where(entry => entry.State == EntityState.Modified)) entry.Entity.AdvanceVersion();
        foreach (var entry in ChangeTracker.Entries<DraftSession>().Where(entry => entry.State == EntityState.Modified)) entry.Entity.AdvanceVersion();
        foreach (var entry in ChangeTracker.Entries<BossActivity>().Where(entry => entry.State == EntityState.Modified)) entry.Entity.AdvanceVersion();
        foreach (var entry in ChangeTracker.Entries<CatalogueItem>().Where(entry => entry.State == EntityState.Modified)) entry.Entity.AdvanceVersion();
        foreach (var entry in ChangeTracker.Entries<SourceDrop>().Where(entry => entry.State == EntityState.Modified)) entry.Entity.AdvanceVersion();
    }
}
