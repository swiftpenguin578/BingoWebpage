using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using Bingo.Application.Access;
using Bingo.Application.Boards;
using Bingo.Application.Catalogue;
using Bingo.Application.Events;
using Bingo.Application.Evidence;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Application.Signups;
using Bingo.Application.Teams;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Infrastructure;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.WiseOldMan;
using Bingo.Web.Boards;
using Bingo.Web.Catalogue;
using Bingo.Web.Events;
using Bingo.Web.HistoricalImport;
using Bingo.Web.Hubs;
using Bingo.Web.Navigation;
using Bingo.Web.Operations;
using Bingo.Web.Security;
using Bingo.Web.Teams;
using Bingo.Web.TestData;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

if (args.Contains("--health-probe", StringComparer.Ordinal))
{
    Environment.ExitCode = await ProbeReadinessAsync();
    return;
}

var resetTestData = args.Contains("--reset-test-data", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsProduction())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole();
}

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
    options.Conventions.AuthorizeFolder("/Captain");
    options.Conventions.ConfigureFilter(new ServiceFilterAttribute(typeof(EventMutationCapabilityPageFilter)));
}).AddDataAnnotationsLocalization(options =>
    options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(Bingo.Web.SharedResource)));
builder.Services.AddInfrastructure(builder.Configuration);
var dataProtection = builder.Services.AddDataProtection();
if (builder.Environment.IsProduction())
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(ProductionPreflight.RequireAbsoluteKeyRingPath(builder.Configuration)));
builder.Services.AddSignalR();
builder.Services.AddScoped<IProgressNotifier, SignalRProgressNotifier>();
builder.Services.AddScoped<IAdminCollaborationNotifier, SignalRAdminCollaborationNotifier>();
builder.Services.AddScoped<ITeamFocusNotifier, SignalRTeamFocusNotifier>();
builder.Services.Configure<DevelopmentAdminBootstrapOptions>(
    builder.Configuration.GetSection(DevelopmentAdminBootstrapOptions.SectionName));
var discordOptions = builder.Configuration.GetSection(DiscordAuthenticationOptions.SectionName).Get<DiscordAuthenticationOptions>() ?? new DiscordAuthenticationOptions();
builder.Services.Configure<DiscordAuthenticationOptions>(builder.Configuration.GetSection(DiscordAuthenticationOptions.SectionName));
builder.Services.AddScoped<IPasswordHasher<Account>, PasswordHasher<Account>>();
builder.Services.AddScoped<AccountAuthenticationService>();
builder.Services.AddScoped<AccountIdentityService>();
builder.Services.AddScoped<MyAccountsService>();
builder.Services.AddSingleton<ISignupLookupTokenService, SignupLookupTokenService>();
builder.Services.AddSingleton<DiscordOnboardingStateService>();
builder.Services.AddScoped<AccountAdministrationService>();
builder.Services.AddScoped<EmergencyCredentialService>();
builder.Services.AddScoped<EmergencyCredentialLifecycleService>();
builder.Services.AddSingleton<DiscordLinkStateService>();
builder.Services.AddSingleton<LoginThrottleService>();
builder.Services.AddScoped<CaptainAccountProvisioner>();
builder.Services.AddScoped<AccountCookieEvents>();
builder.Services.AddScoped<DevelopmentAdminBootstrapper>();
builder.Services.AddScoped<OperatorRecoveryService>();
builder.Services.AddScoped<Bingo.Web.Teams.PreformedRosterCsvImportService>();
builder.Services.Configure<WiseOldManOptions>(builder.Configuration.GetSection(WiseOldManOptions.SectionName));
var wiseOldManHttpClient = builder.Services.AddHttpClient("WiseOldMan", (serviceProvider, client) =>
{
    var settings = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<WiseOldManOptions>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 5, 60));
    client.DefaultRequestHeaders.UserAgent.ParseAdd(settings.UserAgent);
    if (!string.IsNullOrWhiteSpace(settings.ApiKey)) client.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", settings.ApiKey);
});
if (builder.Environment.IsDevelopment())
{
    wiseOldManHttpClient.ConfigurePrimaryHttpMessageHandler(serviceProvider =>
    {
        var settings = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<WiseOldManOptions>>().Value;
        return settings.DevelopmentFake.Enabled && !resetTestData
            ? new WiseOldManDevelopmentFakeHandler(settings, serviceProvider.GetRequiredService<TimeProvider>())
            : new HttpClientHandler();
    });
}
builder.Services.AddSingleton<WiseOldManRequestLimiter>();
builder.Services.AddSingleton<WiseOldManClient>();
builder.Services.AddSingleton<IWiseOldManPlayerLookup>(serviceProvider => serviceProvider.GetRequiredService<WiseOldManClient>());
builder.Services.AddSingleton<IWiseOldManCompetitionClient>(serviceProvider => serviceProvider.GetRequiredService<WiseOldManClient>());
builder.Services.AddSingleton<IWiseOldManStatus>(serviceProvider => serviceProvider.GetRequiredService<WiseOldManClient>());
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
builder.Services.AddScoped<ProductionPreflight>();
builder.Services.AddScoped<DevelopmentScenarioSeeder>();
builder.Services.AddScoped<HistoricalEventImporter>();
builder.Services.AddScoped<SharedShellService>();
builder.Services.AddScoped<PublicTeamImageService>();
builder.Services.AddScoped<PublicBoardImageService>();
builder.Services.AddScoped<EventMutationCapabilityPageFilter>();
builder.Services.AddSingleton<WorkerHeartbeatRegistry>();
builder.Services.AddHostedService<EventLifecycleWorker>();
builder.Services.AddHostedService<EventCompetitionSynchronizationWorker>();
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
        options.SlidingExpiration = false;
        options.EventsType = typeof(AccountCookieEvents);
    });
if (discordOptions.IsConfigured)
{
    builder.Services.AddAuthentication().AddCookie("Discord.External", options => { options.ExpireTimeSpan = TimeSpan.FromMinutes(15); options.Cookie.Name = "Bingo.Discord.External"; })
        .AddOAuth("Discord", options =>
        {
            options.ClientId = discordOptions.ClientId!; options.ClientSecret = discordOptions.ClientSecret!; options.CallbackPath = "/Account/DiscordCallback"; options.SignInScheme = "Discord.External";
            options.AuthorizationEndpoint = "https://discord.com/api/oauth2/authorize"; options.TokenEndpoint = "https://discord.com/api/oauth2/token"; options.UserInformationEndpoint = "https://discord.com/api/users/@me"; options.Scope.Add("identify");
            options.ClaimActions.Add(new JsonKeyClaimAction(System.Security.Claims.ClaimTypes.NameIdentifier, System.Security.Claims.ClaimValueTypes.String, "id"));
            options.ClaimActions.Add(new JsonKeyClaimAction(System.Security.Claims.ClaimTypes.Name, System.Security.Claims.ClaimValueTypes.String, "global_name"));
            options.Events.OnRemoteFailure = context =>
            {
                context.HandleResponse();
                var tempDataFactory = context.HttpContext.RequestServices.GetRequiredService<ITempDataDictionaryFactory>();
                var tempData = tempDataFactory.GetTempData(context.HttpContext);
                var text = context.HttpContext.RequestServices.GetRequiredService<IStringLocalizer<Bingo.Web.SharedResource>>();
                tempData["StatusMessage"] = text["Discord sign-in was cancelled or failed. Please try again."].Value;
                context.HttpContext.RequestServices.GetRequiredService<ITempDataProvider>().SaveTempData(context.HttpContext, tempData);
                var properties = context.Properties ?? (context.Options is OAuthOptions oauth
                    ? oauth.StateDataFormat.Unprotect(context.Request.Query["state"].ToString()) : null);
                var purpose = properties?.Items.TryGetValue("discord-purpose", out var value) == true ? value : null;
                var returnUrl = properties?.Items.TryGetValue("discord-return-url", out var destination) == true ? destination : null;
                context.Response.Redirect(purpose is "link" or "replace" ? "/Account/Settings"
                    : RedirectHttpResult.IsLocalUrl(returnUrl) ? QueryHelpers.AddQueryString("/Account/Login", "ReturnUrl", returnUrl!) : "/Account/Login");
                return Task.CompletedTask;
            };
            options.Events.OnCreatingTicket = async context =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
                using var response = await context.Backchannel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.HttpContext.RequestAborted);
                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException("Discord user information could not be retrieved.");
                await using var stream = await response.Content.ReadAsStreamAsync(context.HttpContext.RequestAborted);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: context.HttpContext.RequestAborted);
                context.RunClaimActions(document.RootElement);
                if (context.Identity?.FindFirst(ClaimTypes.Name) is null && document.RootElement.TryGetProperty("username", out var username) && username.ValueKind == JsonValueKind.String)
                    context.Identity?.AddClaim(new Claim(ClaimTypes.Name, username.GetString()!));
            };
        });
}
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthorizationPolicies.Admin, policy =>
        policy.RequireAuthenticatedUser().RequireRole(GlobalRole.Admin.ToString(), GlobalRole.SuperAdmin.ToString()))
    .AddPolicy(AuthorizationPolicies.SuperAdmin, policy => policy.RequireAuthenticatedUser().RequireRole(GlobalRole.SuperAdmin.ToString()))
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
builder.Services.AddRateLimiter(_ => { });
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>(
        name: "postgresql",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]);
if (builder.Environment.IsProduction())
    builder.Services.AddHealthChecks().AddCheck<ProductionReadinessHealthCheck>("production-operations", tags: ["ready"]);

var app = builder.Build();

var catalogueSnapshotPath = Path.Combine(app.Environment.ContentRootPath, CatalogueSnapshotService.DefaultRelativePath);

if (app.Environment.IsProduction())
    ProductionPreflight.ValidateDataProtection(app.Services, app.Configuration);

var historicalImportRequested = args.Contains("--preflight-historical-import", StringComparer.Ordinal) ||
                                args.Contains("--apply-historical-import", StringComparer.Ordinal);
if (historicalImportRequested)
{
    var inputIndex = Array.IndexOf(args, "--historical-import-input");
    var actorIndex = Array.IndexOf(args, "--historical-import-actor");
    var confirmationIndex = Array.IndexOf(args, "--confirm-historical-import");
    var inputPath = inputIndex >= 0 && inputIndex + 1 < args.Length ? args[inputIndex + 1] : null;
    var actor = actorIndex >= 0 && actorIndex + 1 < args.Length ? args[actorIndex + 1] : null;
    var confirmation = confirmationIndex >= 0 && confirmationIndex + 1 < args.Length ? args[confirmationIndex + 1] : null;
    var apply = args.Contains("--apply-historical-import", StringComparer.Ordinal);
    await using var historicalScope = app.Services.CreateAsyncScope();
    var result = await historicalScope.ServiceProvider.GetRequiredService<HistoricalEventImporter>().RunAsync(
        new HistoricalImportOptions(
            Path.Combine(app.Environment.ContentRootPath, "data", "historical-import", "det-store-danske-sommerbingo-2026.json"),
            inputPath, actor, confirmation, apply), CancellationToken.None);
    Console.WriteLine(result.Summary);
    foreach (var error in result.Errors) Console.Error.WriteLine($"ERROR: {error}");
    if (!result.Succeeded) Environment.ExitCode = 2;
    return;
}

if (args.Contains("--migrate", StringComparer.Ordinal))
{
    await using var migrationScope = app.Services.CreateAsyncScope();
    var migrationDb = migrationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var migrationConnection = migrationDb.Database.GetDbConnection();
    await migrationConnection.OpenAsync();
    try
    {
        var mappingIndex = Array.IndexOf(args, "--immutable-item-mapping");
        var mappingHashIndex = Array.IndexOf(args, "--immutable-item-mapping-sha256");
        var mappingPath = mappingIndex >= 0 && mappingIndex + 1 < args.Length ? args[mappingIndex + 1] : null;
        var mappingHash = mappingHashIndex >= 0 && mappingHashIndex + 1 < args.Length ? args[mappingHashIndex + 1] : null;
        var pending = (await migrationDb.Database.GetPendingMigrationsAsync()).ToList();
        var immutableMigrationIndex = pending.FindIndex(value => value.Contains("AddImmutableCatalogueItemIdentity", StringComparison.Ordinal));
        if (immutableMigrationIndex > 0)
        {
            await migrationDb.Database.MigrateAsync(pending[immutableMigrationIndex - 1], CancellationToken.None);
            pending = (await migrationDb.Database.GetPendingMigrationsAsync()).ToList();
            immutableMigrationIndex = pending.FindIndex(value => value.Contains("AddImmutableCatalogueItemIdentity", StringComparison.Ordinal));
        }
        if (immutableMigrationIndex == 0)
            await Slice1MigrationPreflight.StageImmutableItemMappingsAsync(migrationConnection, mappingPath, mappingHash, CancellationToken.None);
        await migrationDb.Database.MigrateAsync();
    }
    finally { await migrationConnection.CloseAsync(); }
    Console.WriteLine("Database migrations applied.");
    return;
}

if (args.Contains("--immutable-item-preflight", StringComparer.Ordinal))
{
    await using var scope = app.Services.CreateAsyncScope();
    Console.WriteLine(await scope.ServiceProvider.GetRequiredService<Slice1MigrationPreflight>().RunImmutableItemPreflightAsync(CancellationToken.None));
    return;
}

if (args.Contains("--production-preflight", StringComparer.Ordinal))
{
    await using var preflightScope = app.Services.CreateAsyncScope();
    await preflightScope.ServiceProvider.GetRequiredService<ProductionPreflight>().ValidateAsync(catalogueSnapshotPath, CancellationToken.None);
    Console.WriteLine("Production preflight passed.");
    return;
}

if (args.Contains("--legacy-image-rollback-preflight", StringComparer.Ordinal))
{
    await using var rollbackScope = app.Services.CreateAsyncScope();
    await rollbackScope.ServiceProvider.GetRequiredService<ProductionPreflight>().ValidateLegacyImageRollbackAsync(CancellationToken.None);
    Console.WriteLine("Legacy image rollback safety preflight passed.");
    return;
}

if (args.Contains("--export-catalogue-snapshot", StringComparer.Ordinal))
{
    await using var snapshotScope = app.Services.CreateAsyncScope();
    var snapshotDb = snapshotScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await snapshotDb.Database.MigrateAsync();
    var snapshots = snapshotScope.ServiceProvider.GetRequiredService<CatalogueSnapshotService>();
    var result = await snapshots.ExportAsync(catalogueSnapshotPath);
    Console.WriteLine($"Catalogue snapshot exported to {catalogueSnapshotPath}: {result.Bosses} bosses, {result.Items} items, {result.Drops} drops.");
    return;
}

if (args.Contains("--slice1-migration-preflight", StringComparer.Ordinal))
{
    var ownerIndex = Array.IndexOf(args, "--slice1-owner");
    var owner = ownerIndex >= 0 && ownerIndex + 1 < args.Length ? args[ownerIndex + 1] : null;
    await using var scope = app.Services.CreateAsyncScope();
    var report = await scope.ServiceProvider.GetRequiredService<Slice1MigrationPreflight>().RunAsync(owner, CancellationToken.None);
    Console.WriteLine(report); return;
}

if (args.Contains("--slice1-recover-owner", StringComparer.Ordinal))
{
    var usernameIndex = Array.IndexOf(args, "--username");
    var confirmIndex = Array.IndexOf(args, "--confirm-username");
    if (usernameIndex < 0 || usernameIndex + 1 >= args.Length || confirmIndex < 0 || confirmIndex + 1 >= args.Length)
        throw new InvalidOperationException("Owner recovery requires --username <username> and --confirm-username <username>.");
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<OperatorRecoveryService>().RecoverOwnerAsync(args[usernameIndex + 1], args[confirmIndex + 1], CancellationToken.None);
    Console.WriteLine("Super Admin ownership recovery completed.");
    return;
}

if (args.Contains("--slice1-create-owner-reset-link", StringComparer.Ordinal))
{
    var usernameIndex = Array.IndexOf(args, "--username");
    var confirmIndex = Array.IndexOf(args, "--confirm-username");
    var baseUrlIndex = Array.IndexOf(args, "--base-url");
    if (usernameIndex < 0 || usernameIndex + 1 >= args.Length || confirmIndex < 0 || confirmIndex + 1 >= args.Length || baseUrlIndex < 0 || baseUrlIndex + 1 >= args.Length ||
        !Uri.TryCreate(args[baseUrlIndex + 1], UriKind.Absolute, out var baseUrl) || baseUrl.Scheme is not ("http" or "https"))
        throw new InvalidOperationException("Owner reset-link creation requires --username <owner>, --confirm-username <owner>, and --base-url <absolute-url>.");
    await using var scope = app.Services.CreateAsyncScope();
    var token = await scope.ServiceProvider.GetRequiredService<OperatorRecoveryService>().CreateOwnerRecoveryResetLinkAsync(args[usernameIndex + 1], args[confirmIndex + 1], CancellationToken.None);
    Console.WriteLine(new Uri(baseUrl, $"/Account/ResetPassword/{token}").ToString());
    return;
}

if (args.Contains("--slice1-promote-retained-owner", StringComparer.Ordinal))
{
    var usernameIndex = Array.IndexOf(args, "--username"); var confirmIndex = Array.IndexOf(args, "--confirm-username");
    if (usernameIndex < 0 || usernameIndex + 1 >= args.Length || confirmIndex < 0 || confirmIndex + 1 >= args.Length) throw new InvalidOperationException("Retained owner promotion requires --username and --confirm-username.");
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<OperatorRecoveryService>().PromoteRetainedOwnerAsync(args[usernameIndex + 1], args[confirmIndex + 1], CancellationToken.None);
    Console.WriteLine("Preflight-selected retained Admin promoted to Super Admin."); return;
}

if (args.Contains("--slice1-bootstrap-owner", StringComparer.Ordinal))
{
    var usernameIndex = Array.IndexOf(args, "--username");
    var confirmIndex = Array.IndexOf(args, "--confirm-username");
    var password = app.Configuration["Slice1:BootstrapOwnerPassword"];
    if (usernameIndex < 0 || usernameIndex + 1 >= args.Length || string.IsNullOrWhiteSpace(password) || confirmIndex < 0 || confirmIndex + 1 >= args.Length)
        throw new InvalidOperationException("Owner bootstrap requires --username <username>, --confirm-username <username>, and the Slice1:BootstrapOwnerPassword secret.");
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<OperatorRecoveryService>().BootstrapOwnerAsync(args[usernameIndex + 1], password, args[confirmIndex + 1], CancellationToken.None);
    Console.WriteLine("Initial Super Admin bootstrap completed.");
    return;
}

if (args.Contains("--apply-catalogue-snapshot", StringComparer.Ordinal))
{
    await using var snapshotScope = app.Services.CreateAsyncScope();
    var snapshotDb = snapshotScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (!app.Environment.IsProduction()) await snapshotDb.Database.MigrateAsync();
    var snapshots = snapshotScope.ServiceProvider.GetRequiredService<CatalogueSnapshotService>();
    var result = await snapshots.ApplyAsync(catalogueSnapshotPath);
    Console.WriteLine($"Catalogue snapshot applied from {catalogueSnapshotPath}: {result.Bosses} bosses, {result.Items} items, {result.Drops} drops.");
    return;
}

if (app.Environment.IsProduction())
{
    await using var preflightScope = app.Services.CreateAsyncScope();
    await preflightScope.ServiceProvider.GetRequiredService<ProductionPreflight>().ValidateAsync(catalogueSnapshotPath, CancellationToken.None);
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

    SeedResult result;
    await using (var seedScope = app.Services.CreateAsyncScope())
    {
        var seedDb = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await seedDb.Database.MigrateAsync();
        var seeder = seedScope.ServiceProvider.GetRequiredService<DevelopmentScenarioSeeder>();
        result = await seeder.ResetAndSeedAsync();
    }

    Console.WriteLine($"Test database reset complete. Preserved admin: {result.AdminUsername}");
    Console.WriteLine($"Second admin for concurrency tests: {result.SecondaryAdminUsername} / {result.SecondaryAdminPassword}");
    Console.WriteLine($"Global-only Admin: {result.GlobalAdminUsername} / {result.GlobalAdminPassword}");
    Console.WriteLine($"Admin+Participant: {result.AdminParticipantUsername} / {result.AdminParticipantPassword}");
    Console.WriteLine($"Admin+Captain: {result.AdminCaptainUsername} / {result.AdminCaptainPassword}");
    Console.WriteLine($"Admin+Co-captain: {result.AdminCoCaptainUsername} / {result.AdminCoCaptainPassword}");
    Console.WriteLine($"Waiting-list replacement account: {result.ReplacementUsername} / {result.ReplacementPassword}");
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
        var returnUrl = context.Request.GetEncodedPathAndQuery();
        context.Response.Redirect(QueryHelpers.AddQueryString("/Account/ChangePassword", "ReturnUrl", RedirectHttpResult.IsLocalUrl(returnUrl) ? returnUrl : "/"));
        return;
    }

    await next();
});
app.Use(async (context, next) =>
{
    await next();

    var enhancedPostMode = context.Request.Headers["X-Bingo-Enhanced-Post"].ToString();
    var isFullNavigation = string.Equals(enhancedPostMode, "true", StringComparison.OrdinalIgnoreCase);
    var isPartialUpdate = string.Equals(enhancedPostMode, "partial", StringComparison.OrdinalIgnoreCase);
    if (context.Response.HasStarted ||
        !HttpMethods.IsPost(context.Request.Method) ||
        !(isFullNavigation || isPartialUpdate) ||
        context.Response.StatusCode is < StatusCodes.Status300MultipleChoices or >= StatusCodes.Status400BadRequest ||
        !context.Response.Headers.TryGetValue("Location", out var location) ||
        string.IsNullOrWhiteSpace(location))
    {
        return;
    }

    if (isPartialUpdate)
    {
        var requestUri = new Uri($"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}");
        if (!Uri.TryCreate(requestUri, location.ToString(), out var destination) ||
            string.Equals(requestUri.AbsolutePath, destination.AbsolutePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
    }

    context.Response.Headers["X-Bingo-Post-Navigation"] = location.ToString();
    context.Response.Headers.Remove("Location");
    context.Response.StatusCode = StatusCodes.Status204NoContent;
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
app.MapGet("/Events/{slug}/Teams/{teamId:guid}/Image", (string slug, Guid teamId, PublicTeamImageService images, CancellationToken cancellationToken) =>
    images.OpenAsync(slug, teamId, cancellationToken));
app.MapGet("/Events/{slug}/Board/Tiles/{tileId:guid}/Image", (string slug, Guid tileId, PublicBoardImageService images, CancellationToken cancellationToken) =>
    images.OpenAsync(slug, tileId, cancellationToken));
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
app.MapHub<TeamFocusHub>("/hubs/team-focus");

app.Run();

static async Task<int> ProbeReadinessAsync()
{
    try
    {
        using var handler = new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2) };
        using var response = await client.GetAsync("http://127.0.0.1:8080/health/ready", HttpCompletionOption.ResponseHeadersRead);
        return response.IsSuccessStatusCode ? 0 : 1;
    }
    catch
    {
        return 1;
    }
}

public partial class Program;
