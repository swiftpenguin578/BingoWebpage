using System.Globalization;
using System.Threading.RateLimiting;
using Bingo.Application.Access;
using Bingo.Application.Boards;
using Bingo.Application.Catalogue;
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
builder.Services.AddHttpClient("OsrsWikiImages", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("OSRSCommunityBingo/1.0 (catalogue image cache)");
});
builder.Services.AddSingleton<OsrsWikiImageCache>();
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

if (args.Contains("--sync-catalogue-images", StringComparer.Ordinal))
{
    await using var imageScope = app.Services.CreateAsyncScope();
    var imageDb = imageScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await imageDb.Database.MigrateAsync();
    var cache = imageScope.ServiceProvider.GetRequiredService<OsrsWikiImageCache>();
    var sources = (await imageDb.BossActivities.AsNoTracking().Where(value => value.ImageUrl != null).Select(value => value.ImageUrl!).ToListAsync())
        .Concat(await imageDb.CatalogueItems.AsNoTracking().Where(value => value.ImageUrl != null).Select(value => value.ImageUrl!).ToListAsync())
        .Concat(await imageDb.TileTemplates.AsNoTracking().Where(value => value.ImageUrl != null).Select(value => value.ImageUrl!).ToListAsync())
        .Concat(await imageDb.BoardTiles.AsNoTracking().Where(value => value.ImageUrlSnapshot != null).Select(value => value.ImageUrlSnapshot!).ToListAsync())
        .Select(OsrsWikiImageUrl.Normalize)
        .Where(value => value is not null)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
    var cached = 0;
    var failed = 0;
    var syncDelayMilliseconds = Math.Max(250, app.Configuration.GetValue<int?>("CatalogueImageCache:SyncDelayMilliseconds") ?? 500);
    Console.WriteLine($"Synchronizing {sources.Count} catalogue images with a {syncDelayMilliseconds} ms delay between sources.");
    for (var sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
    {
        var source = sources[sourceIndex]!;
        if (cache.IsCached(source))
        {
            cached++;
            continue;
        }

        var synchronized = false;
        for (var attempt = 1; attempt <= 3 && !synchronized; attempt++)
        {
            try
            {
                await cache.GetAsync(source);
                cached++;
                synchronized = true;
            }
            catch (HttpRequestException exception) when (
                attempt < 3 && exception.StatusCode is System.Net.HttpStatusCode.TooManyRequests or System.Net.HttpStatusCode.ServiceUnavailable)
            {
                var retryDelay = TimeSpan.FromSeconds(attempt * 10);
                Console.Error.WriteLine($"Wiki temporarily unavailable for {source}; retrying in {retryDelay.TotalSeconds:0} seconds.");
                await Task.Delay(retryDelay);
            }
            catch (Exception exception)
            {
                failed++;
                Console.Error.WriteLine($"Could not cache {source}: {exception.Message}");
                break;
            }
        }

        if (sourceIndex < sources.Count - 1) await Task.Delay(syncDelayMilliseconds);
    }
    Console.WriteLine($"Catalogue image synchronization complete. Cached: {cached}; failed: {failed}.");
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
app.MapGet(OsrsWikiImageCache.EndpointPath, async (string source, HttpContext context, OsrsWikiImageCache cache, CancellationToken cancellationToken) =>
{
    try
    {
        var cached = await cache.GetAsync(source, cancellationToken);
        context.Response.Headers.CacheControl = "public,max-age=604800";
        return Results.File(cached.Path, cached.MediaType, enableRangeProcessing: true);
    }
    catch (InvalidOperationException)
    {
        return Results.BadRequest();
    }
    catch (HttpRequestException)
    {
        var fallback = OsrsWikiImageUrl.Normalize(source);
        return Uri.TryCreate(fallback, UriKind.Absolute, out var uri) && uri.Host == "oldschool.runescape.wiki"
            ? Results.Redirect(fallback)
            : Results.NotFound();
    }
});
app.MapRazorPages()
   .WithStaticAssets();
app.MapHub<ProgressHub>("/hubs/progress");
app.MapHub<AdminCollaborationHub>("/hubs/admin-collaboration");

app.Run();

public partial class Program;
