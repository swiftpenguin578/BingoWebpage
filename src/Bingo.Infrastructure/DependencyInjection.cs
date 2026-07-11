using Bingo.Application.Auditing;
using Bingo.Application.Security;
using Bingo.Application.Signups;
using Bingo.Infrastructure.Auditing;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Security;
using Bingo.Infrastructure.Signups;
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
        services.AddSingleton<ISecretHasher, SecretHasher>();
        services.AddSingleton<IPrivateEditTokenService, PrivateEditTokenService>();
        services.AddScoped<ISignupService, SignupService>();

        return services;
    }
}
