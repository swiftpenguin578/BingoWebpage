using Bingo.Application.Auditing;
using Bingo.Application.Boards;
using Bingo.Application.Events;
using Bingo.Application.Evidence;
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
        services.AddSingleton<IPrivateEditTokenService, PrivateEditTokenService>();
        services.AddScoped<ISignupService, SignupService>();
        services.AddScoped<EventParticipantCharacterService>();
        if (string.Equals(configuration["EvidenceStorage:Provider"], "R2", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IEvidenceStorage, R2EvidenceStorage>();
        else
            services.AddSingleton<IEvidenceStorage, LocalEvidenceStorage>();
        services.AddScoped<ISubmissionService, SubmissionService>();
        services.AddScoped<IPublicBoardService, PublicBoardService>();
        services.AddScoped<IEventFinalizationService, EventFinalizationService>();
        services.AddScoped<IEventReadinessEvaluator, EventReadinessEvaluator>();
        services.AddScoped<IEventSignupLifecycleService, EventSignupLifecycleService>();
        services.AddScoped<IEventLifecycleService, EventLifecycleService>();
        services.AddScoped<IEventDestructiveLifecycleService, EventDestructiveLifecycleService>();
        services.AddScoped<IEventBannerCleanupService, EventBannerCleanupService>();
        services.AddScoped<IProgressNotifier, NullProgressNotifier>();
        services.AddScoped<IAdminCollaborationNotifier, NullAdminCollaborationNotifier>();

        return services;
    }
}
