using System.Globalization;
using System.Threading.RateLimiting;
using Bingo.Application.Access;
using Bingo.Application.Boards;
using Bingo.Application.Teams;
using Bingo.Domain.Access;
using Bingo.Infrastructure;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Catalogue;
using Bingo.Web.Events;
using Bingo.Web.Hubs;
using Bingo.Web.Navigation;
using Bingo.Web.Security;
using Bingo.Web.TestData;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { new CultureInfo("en"), new CultureInfo("da") };
    options.DefaultRequestCulture = new RequestCulture("en");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders =
    [
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    ];
});
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin", AuthorizationPolicies.Admin);
    options.Conventions.AuthorizeFolder("/Captain", AuthorizationPolicies.CaptainCorrectionAccess);
});
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddScoped<IProgressNotifier, SignalRProgressNotifier>();
builder.Services.AddScoped<IAdminCollaborationNotifier, SignalRAdminCollaborationNotifier>();
builder.Services.Configure<DevelopmentAdminBootstrapOptions>(
    builder.Configuration.GetSection(DevelopmentAdminBootstrapOptions.SectionName));
builder.Services.AddScoped<IPasswordHasher<Account>, PasswordHasher<Account>>();
builder.Services.AddScoped<AccountAuthenticationService>();
builder.Services.AddScoped<CaptainAccountProvisioner>();
builder.Services.AddScoped<AccountCookieEvents>();
builder.Services.AddScoped<DevelopmentAdminBootstrapper>();
builder.Services.AddScoped<ClanCatalogueImporter>();
builder.Services.AddHttpClient("OsrsWiki", client =>
{
    client.BaseAddress = new Uri("https://oldschool.runescape.wiki/");
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("OSRSCommunityBingo/1.0 (catalogue dry-run)");
});
builder.Services.AddScoped<OsrsWikiCatalogueDryRunService>();
builder.Services.AddScoped<CatalogueSnapshotService>();
builder.Services.AddScoped<DevelopmentScenarioSeeder>();
builder.Services.AddScoped<SharedShellService>();
builder.Services.AddHostedService<EventLifecycleWorker>();
builder.Services.AddScoped<IAuthorizationHandler, AccountAuthorizationHandler>();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "Bingo.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
        options.EventsType = typeof(AccountCookieEvents);
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthorizationPolicies.Admin, policy =>
        policy.RequireAuthenticatedUser().RequireRole(AccountRole.Admin.ToString()))
    .AddPolicy(AuthorizationPolicies.Captain, policy =>
        policy.RequireAuthenticatedUser()
            .RequireRole(AccountRole.Captain.ToString())
            .RequireClaim(AccountClaims.EventId)
            .RequireClaim(AccountClaims.TeamId)
            .AddRequirements(new AccountAccessRequirement(AccountAccessMode.CorrectionOnly)))
    .AddPolicy(AuthorizationPolicies.CaptainFullAccess, policy =>
        policy.RequireAuthenticatedUser()
            .RequireRole(AccountRole.Captain.ToString())
            .RequireClaim(AccountClaims.EventId)
            .RequireClaim(AccountClaims.TeamId)
            .AddRequirements(new AccountAccessRequirement(AccountAccessMode.Full)))
    .AddPolicy(AuthorizationPolicies.CaptainCorrectionAccess, policy =>
        policy.RequireAuthenticatedUser()
            .RequireRole(AccountRole.Captain.ToString())
            .RequireClaim(AccountClaims.EventId)
            .RequireClaim(AccountClaims.TeamId)
            .AddRequirements(new AccountAccessRequirement(AccountAccessMode.CorrectionOnly)))
    .AddPolicy(AuthorizationPolicies.CaptainTeamScoped, policy =>
        policy.RequireAuthenticatedUser()
            .RequireRole(AccountRole.Captain.ToString())
            .AddRequirements(
                new AccountAccessRequirement(AccountAccessMode.CorrectionOnly),
                new TeamScopeRequirement()));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("login", limiter =>
    {
        limiter.PermitLimit = 5;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
});
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>(
        name: "postgresql",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]);

var app = builder.Build();

var catalogueSnapshotPath = Path.Combine(app.Environment.ContentRootPath, CatalogueSnapshotService.DefaultRelativePath);

if (args.Contains("--export-catalogue-snapshot", StringComparer.Ordinal))
{
    await using var snapshotScope = app.Services.CreateAsyncScope();
    var snapshotDb = snapshotScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await snapshotDb.Database.MigrateAsync();
    var snapshots = snapshotScope.ServiceProvider.GetRequiredService<CatalogueSnapshotService>();
    var result = await snapshots.ExportAsync(catalogueSnapshotPath);
    Console.WriteLine($"Catalogue snapshot exported to {catalogueSnapshotPath}: {result.Bosses} bosses, {result.Items} items, {result.Drops} drops, {result.Variants} variants.");
    return;
}

if (args.Contains("--apply-catalogue-snapshot", StringComparer.Ordinal))
{
    await using var snapshotScope = app.Services.CreateAsyncScope();
    var snapshotDb = snapshotScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await snapshotDb.Database.MigrateAsync();
    var snapshots = snapshotScope.ServiceProvider.GetRequiredService<CatalogueSnapshotService>();
    var result = await snapshots.ApplyAsync(catalogueSnapshotPath);
    Console.WriteLine($"Catalogue snapshot applied from {catalogueSnapshotPath}: {result.Bosses} bosses, {result.Items} items, {result.Drops} drops, {result.Variants} variants.");
    return;
}

if (args.Contains("--apply-wiki-catalogue", StringComparer.Ordinal))
{
    await using var importScope = app.Services.CreateAsyncScope();
    var importDb = importScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await importDb.Database.MigrateAsync();
    var importer = importScope.ServiceProvider.GetRequiredService<OsrsWikiCatalogueDryRunService>();
    var result = await importer.ApplyReviewedImportAsync();
    Console.WriteLine($"Wiki catalogue import complete. Bosses updated: {result.BossesUpdated}; drops added: {result.DropsAdded}; drops updated: {result.DropsUpdated}; old drops removed: {result.DropsRemoved}; orphaned items removed: {result.OrphanedItemsRemoved}; review flags retained: {result.ReviewCount}.");
    return;
}

if (args.Contains("--reset-test-data", StringComparer.Ordinal))
{
    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("--reset-test-data can be used only in the Development environment.");
    }

    await using var seedScope = app.Services.CreateAsyncScope();
    var seedDb = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await seedDb.Database.MigrateAsync();
    var seeder = seedScope.ServiceProvider.GetRequiredService<DevelopmentScenarioSeeder>();
    var result = await seeder.ResetAndSeedAsync();
    Console.WriteLine($"Test database reset complete. Preserved admin: {result.AdminUsername}");
    Console.WriteLine($"Second admin for concurrency tests: {result.SecondaryAdminUsername} / {result.SecondaryAdminPassword}");
    Console.WriteLine($"Board blueprint: {result.BoardBlueprint}");
    foreach (var scenario in result.Scenarios)
    {
        Console.WriteLine($"- {scenario.EventName} [{scenario.EventState}; board: {scenario.BoardState?.ToString() ?? "none"}]");
        foreach (var username in scenario.CaptainUsernames)
        {
            Console.WriteLine($"  Captain login: {username} / {result.CaptainPassword}");
        }
    }
    return;
}

var clanCatalogueArgument = args.SkipWhile(value => !string.Equals(value, "--import-clan-catalogue", StringComparison.Ordinal)).Skip(1).FirstOrDefault();
if (clanCatalogueArgument is not null)
{
    await using var importScope = app.Services.CreateAsyncScope(); var importer = importScope.ServiceProvider.GetRequiredService<ClanCatalogueImporter>(); var result = await importer.ImportAsync(clanCatalogueArgument);
    Console.WriteLine($"Clan catalogue import complete. Bosses created: {result.BossesCreated}; drops created: {result.DropsCreated}; skipped: {result.Skipped.Count}.");
    if (result.Skipped.Count > 0) Console.WriteLine($"Skipped rows: {string.Join("; ", result.Skipped)}");
    return;
}

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var bootstrapper = scope.ServiceProvider.GetRequiredService<DevelopmentAdminBootstrapper>();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await bootstrapper.BootstrapAsync(dbContext);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Errors/{0}");

app.UseHttpsRedirection();

app.UseRequestLocalization();
app.UseRouting();

app.UseRateLimiter();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    var mustChangePassword = context.User.FindFirst(AccountClaims.MustChangePassword)?.Value;
    var path = context.Request.Path;
    if (context.User.Identity?.IsAuthenticated == true &&
        string.Equals(mustChangePassword, bool.TrueString, StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWithSegments("/Account/ChangePassword") &&
        !path.StartsWithSegments("/Account/Logout") &&
        !Path.HasExtension(path))
    {
        context.Response.Redirect("/Account/ChangePassword");
        return;
    }

    await next();
});
app.UseAuthorization();

app.MapStaticAssets();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});
app.MapRazorPages()
   .WithStaticAssets();
app.MapHub<ProgressHub>("/hubs/progress");
app.MapHub<AdminCollaborationHub>("/hubs/admin-collaboration");

app.Run();

public partial class Program;
