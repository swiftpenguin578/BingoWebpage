using System.Threading.RateLimiting;
using Bingo.Application.Access;
using Bingo.Domain.Access;
using Bingo.Infrastructure;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin", AuthorizationPolicies.Admin);
    options.Conventions.AuthorizeFolder("/Captain", AuthorizationPolicies.CaptainCorrectionAccess);
});
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<DevelopmentAdminBootstrapOptions>(
    builder.Configuration.GetSection(DevelopmentAdminBootstrapOptions.SectionName));
builder.Services.AddScoped<IPasswordHasher<Account>, PasswordHasher<Account>>();
builder.Services.AddScoped<AccountAuthenticationService>();
builder.Services.AddScoped<AccountCookieEvents>();
builder.Services.AddScoped<DevelopmentAdminBootstrapper>();
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

app.UseHttpsRedirection();

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

app.Run();

public partial class Program;
