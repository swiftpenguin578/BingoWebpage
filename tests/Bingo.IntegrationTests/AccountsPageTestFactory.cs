using System.Security.Claims;
using Bingo.Domain.Access;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Pages.Admin.Accounts;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

namespace Bingo.IntegrationTests;

/// <summary>Builds the Accounts directory page model (A10: replaces the retired Manage model in projection tests).</summary>
internal static class AccountsPageTestFactory
{
    public static IndexModel Create(ApplicationDbContext db, Guid? actorId = null, string? q = null, string? role = null, string? page = null, string? account = null, TimeProvider? time = null)
    {
        time ??= TimeProvider.System;
        var passwords = new PasswordHasher<Account>();
        var context = new DefaultHttpContext();
        if (actorId is { } id) context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "test"));
        return new IndexModel(db, new AccountAdministrationService(db, passwords, time), new AccountIdentityService(db, passwords, time), new DenyAuthorization(), time)
        {
            PageContext = new PageContext(new ActionContext(context, new RouteData(), new PageActionDescriptor())),
            TempData = new TempDataDictionary(context, new NullTempData()),
            Query = q,
            RoleQuery = role,
            PageQuery = page,
            AccountQuery = account
        };
    }

    private sealed class DenyAuthorization : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements) => Task.FromResult(AuthorizationResult.Failed());
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName) => Task.FromResult(AuthorizationResult.Failed());
    }

    private sealed class NullTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
