using Bingo.Application.Auditing;
using Bingo.Application.Boards;
using Bingo.Application.Events;
using Bingo.Application.Evidence;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Application.Security;
using Bingo.Application.Signups;
using Bingo.Application.Teams;
using Bingo.Infrastructure.Auditing;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Security;
using Bingo.Infrastructure.Signups;
using Bingo.Infrastructure.Teams;
using Bingo.Infrastructure.WiseOldMan;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bingo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "The required connection string 'ConnectionStrings:Database' is missing.");
        }

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<Slice1MigrationPreflight>();
        services.AddSingleton<ISecretHasher, SecretHasher>();
        services.AddScoped<ISignupService, SignupService>();
        services.AddScoped<EventParticipantCharacterService>();
        services.AddScoped<IParticipantLiveService, ParticipantLiveService>();
        if (string.Equals(configuration["EvidenceStorage:Provider"], "R2", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<R2EvidenceStorage>();
            services.AddSingleton<IEvidenceStorage>(serviceProvider => serviceProvider.GetRequiredService<R2EvidenceStorage>());
        }
        else
            services.AddSingleton<IEvidenceStorage, LocalEvidenceStorage>();
        services.AddScoped<ISubmissionService, SubmissionService>();
        services.AddScoped<IEvidenceAuthority, EvidenceAuthority>();
        services.AddScoped<IPublicBoardService, PublicBoardService>();
        services.AddScoped<IEventFinalizationService, EventFinalizationService>();
        services.AddScoped<IEventReadinessEvaluator, EventReadinessEvaluator>();
        services.AddScoped<IEventSignupLifecycleService, EventSignupLifecycleService>();
        services.AddScoped<IEventLifecycleService, EventLifecycleService>();
        services.AddScoped<IEventQuarantineService, EventQuarantineService>();
        services.AddScoped<IEventDestructiveLifecycleService, EventDestructiveLifecycleService>();
        services.AddScoped<IEventCompetitionSynchronizationService, EventCompetitionSynchronizationService>();
        services.AddScoped<IEventCompetitionActivityProjection, CachedEventCompetitionActivityProjection>();
        services.AddScoped<IEventBannerCleanupService, EventBannerCleanupService>();
        services.AddScoped<ITeamCaptainAuthorityService, TeamCaptainAuthorityService>();
        services.AddScoped<ITeamFocusService, TeamFocusService>();

        return services;
    }
}
