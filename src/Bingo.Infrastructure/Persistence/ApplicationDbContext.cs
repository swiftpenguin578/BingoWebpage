using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AdvanceAccountVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AdvanceAccountVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void AdvanceAccountVersions()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries<Account>().Where(entry => entry.State == EntityState.Modified))
            entry.Entity.AdvanceVersion();
        foreach (var entry in ChangeTracker.Entries<AccountOsrsCharacter>().Where(entry => entry.State == EntityState.Modified))
            entry.Entity.AdvanceVersion();
        foreach (var entry in ChangeTracker.Entries<EventParticipantCharacter>().Where(entry => entry.State == EntityState.Modified))
            entry.Entity.AdvanceVersion();
    }
    public DbSet<SystemMetadata> SystemMetadata => Set<SystemMetadata>();

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<AccountEventAccess> AccountEventAccesses => Set<AccountEventAccess>();
    public DbSet<PersonalNotification> PersonalNotifications => Set<PersonalNotification>();
    public DbSet<PasswordCredentialToken> PasswordCredentialTokens => Set<PasswordCredentialToken>();
    public DbSet<AccountDiscordIdentityTransition> AccountDiscordIdentityTransitions => Set<AccountDiscordIdentityTransition>();
    public DbSet<OsrsCharacter> OsrsCharacters => Set<OsrsCharacter>();
    public DbSet<AccountOsrsCharacter> AccountOsrsCharacters => Set<AccountOsrsCharacter>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<BingoEvent> Events => Set<BingoEvent>();
    public DbSet<EventStateTransition> EventStateTransitions => Set<EventStateTransition>();
    public DbSet<EventFinalizationSnapshot> EventFinalizations => Set<EventFinalizationSnapshot>();
    public DbSet<OfficialPlacementSnapshot> OfficialPlacements => Set<OfficialPlacementSnapshot>();
    public DbSet<FinalReviewResolution> FinalReviewResolutions => Set<FinalReviewResolution>();
    public DbSet<TeamCompletionCorrection> TeamCompletionCorrections => Set<TeamCompletionCorrection>();
    public DbSet<EventParticipant> EventParticipants => Set<EventParticipant>();
    public DbSet<EventParticipantCharacter> EventParticipantCharacters => Set<EventParticipantCharacter>();
    public DbSet<SignupQuestion> SignupQuestions => Set<SignupQuestion>();
    public DbSet<SignupAnswer> SignupAnswers => Set<SignupAnswer>();
    public DbSet<BossActivity> BossActivities => Set<BossActivity>();
    public DbSet<CatalogueItem> CatalogueItems => Set<CatalogueItem>();
    public DbSet<SourceDrop> SourceDrops => Set<SourceDrop>();
    public DbSet<SourceDropRateVariant> SourceDropRateVariants => Set<SourceDropRateVariant>();
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<TileTemplate> TileTemplates => Set<TileTemplate>();
    public DbSet<TileTemplateRequirement> TileTemplateRequirements => Set<TileTemplateRequirement>();
    public DbSet<TemplateRequirementBoss> TemplateRequirementBosses => Set<TemplateRequirementBoss>();
    public DbSet<TemplateRequirementDrop> TemplateRequirementDrops => Set<TemplateRequirementDrop>();
    public DbSet<BoardTile> BoardTiles => Set<BoardTile>();
    public DbSet<BoardRequirementSnapshot> BoardRequirementSnapshots => Set<BoardRequirementSnapshot>();
    public DbSet<BoardRequirementBossSnapshot> BoardRequirementBossSnapshots => Set<BoardRequirementBossSnapshot>();
    public DbSet<BoardRequirementDropSnapshot> BoardRequirementDropSnapshots => Set<BoardRequirementDropSnapshot>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMembership> TeamMemberships => Set<TeamMembership>();
    public DbSet<DraftSession> DraftSessions => Set<DraftSession>();
    public DbSet<DraftPick> DraftPicks => Set<DraftPick>();
    public DbSet<EvidenceCode> EvidenceCodes => Set<EvidenceCode>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<EvidenceAsset> EvidenceAssets => Set<EvidenceAsset>();
    public DbSet<ReviewAction> ReviewActions => Set<ReviewAction>();
    public DbSet<SubmissionContribution> SubmissionContributions => Set<SubmissionContribution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

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
            entity.Property(account => account.AccountType).HasColumnName("account_type").HasConversion<string>().HasMaxLength(30);
            entity.Property(account => account.GlobalRole).HasColumnName("global_role").HasConversion<string>().HasMaxLength(30);
            entity.Property(account => account.LoginName).HasColumnName("login_name").HasMaxLength(100);
            entity.Property(account => account.NormalizedLoginName).HasColumnName("normalized_login_name").HasMaxLength(100);
            entity.HasIndex(account => account.NormalizedLoginName).IsUnique();
            entity.Property(account => account.PublicUsername).HasColumnName("public_username").HasMaxLength(100);
            entity.Property(account => account.NormalizedPublicUsername).HasColumnName("normalized_public_username").HasMaxLength(100);
            entity.Property(account => account.EmergencyLoginUsername).HasColumnName("emergency_login_username").HasMaxLength(100);
            entity.Property(account => account.DiscordUserId).HasColumnName("discord_user_id").HasMaxLength(100);
            entity.HasIndex(account => account.DiscordUserId).IsUnique().HasFilter("discord_user_id IS NOT NULL");
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
            entity.Property(account => account.Version).HasColumnName("version").IsConcurrencyToken();
            entity.HasIndex(account => account.GlobalRole).IsUnique().HasFilter("global_role = 'SuperAdmin'");
            entity.ToTable(table => table.HasCheckConstraint("ck_accounts_type_role", "(account_type = 'WebsiteAccount' AND global_role IS NOT NULL) OR (account_type = 'EmergencyCaptain' AND global_role IS NULL)"));
        });

        modelBuilder.Entity<AccountEventAccess>(entity => { entity.ToTable("account_event_accesses"); entity.HasKey(x => x.Id); entity.HasIndex(x => x.AccountId); entity.HasIndex(x => new { x.EventId, x.TeamId }); entity.Property(x => x.ActiveFrom).HasColumnName("active_from"); entity.Property(x => x.CorrectionOnlyFrom).HasColumnName("correction_only_from"); entity.Property(x => x.ExpiresAt).HasColumnName("expires_at"); entity.Property(x => x.CutoffDisabled).HasColumnName("cutoff_disabled"); });
        modelBuilder.Entity<PersonalNotification>(entity => { entity.ToTable("personal_notifications"); entity.HasKey(x => x.Id); entity.HasIndex(x => new { x.RecipientAccountId, x.ReadAt, x.CreatedAt }); entity.Property(x => x.Title).HasMaxLength(200); entity.Property(x => x.Detail).HasMaxLength(1_000); entity.Property(x => x.Route).HasMaxLength(500); });
        modelBuilder.Entity<PasswordCredentialToken>(entity => { entity.ToTable("password_credential_tokens"); entity.HasKey(x => x.Id); entity.HasIndex(x => x.TokenHash).IsUnique(); entity.HasIndex(x => new { x.AccountId, x.Purpose }); entity.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(30); entity.Property(x => x.TokenHash).HasMaxLength(200); });
        modelBuilder.Entity<AccountDiscordIdentityTransition>(entity => { entity.ToTable("account_discord_identity_transitions"); entity.HasKey(x => x.Id); entity.HasIndex(x => new { x.AccountId, x.OccurredAt }); entity.Property(x => x.Action).HasMaxLength(30); });
        modelBuilder.Entity<OsrsCharacter>(entity => { entity.ToTable("osrs_characters"); entity.HasKey(x => x.Id); entity.Property(x => x.DisplayName).HasMaxLength(100); entity.Property(x => x.NormalizedName).HasMaxLength(100); entity.HasIndex(x => x.NormalizedName).IsUnique(); });
        modelBuilder.Entity<AccountOsrsCharacter>(entity =>
        {
            entity.ToTable("account_osrs_characters");
            entity.HasKey(x => x.Id);
            entity.Ignore(x => x.SortOrder);
            entity.Property(x => x.Active).HasColumnName("active");
            entity.Property(x => x.Preferred).HasColumnName("preferred");
            entity.Property(x => x.LinkedAt).HasColumnName("linked_at");
            entity.Property(x => x.LinkedByAccountId).HasColumnName("linked_by_account_id");
            entity.Property(x => x.UnlinkedAt).HasColumnName("unlinked_at");
            entity.Property(x => x.PersonalLabel).HasColumnName("personal_label").HasMaxLength(100);
            entity.Property(x => x.Position).HasColumnName("sort_order");
            entity.Property(x => x.SavedEhb).HasColumnName("saved_ehb").HasPrecision(12, 2);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
            entity.HasIndex(x => new { x.AccountId, x.OsrsCharacterId }).IsUnique();
            entity.HasIndex(x => x.AccountId).IsUnique().HasFilter("active AND preferred");
            entity.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(x => x.LinkedByAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<OsrsCharacter>().WithMany().HasForeignKey(x => x.OsrsCharacterId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("ck_account_osrs_characters_active_history", "(active AND unlinked_at IS NULL) OR (NOT active AND unlinked_at IS NOT NULL)");
                table.HasCheckConstraint("ck_account_osrs_characters_saved_ehb", "saved_ehb IS NULL OR saved_ehb >= 0");
                table.HasCheckConstraint("ck_account_osrs_characters_sort_order", "sort_order >= 0");
            });
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
            entity.HasIndex(entry => entry.OccurredAt);
            entity.HasIndex(entry => new { entry.Action, entry.TargetType });
            entity.HasIndex(entry => new { entry.EventId, entry.OccurredAt });
        });
    }
}
