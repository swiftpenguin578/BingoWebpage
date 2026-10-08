using System.Net;
using System.Text.RegularExpressions;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bingo.IntegrationTests;

public sealed partial class AdminDesignShellIntegrationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task IdentitySaveAndCurrentRedirectLostOrDisabledSessionWithoutData(bool disabled, bool post)
    {
        var admin = Admin();
        var item = Event(admin, EventState.Draft, "Protected identity", 2);
        await using (var db = new ApplicationDbContext(options)) { db.AddRange(admin, item); await db.SaveChangesAsync(); }
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Testing").UseSetting("ConnectionStrings:Database", database.GetConnectionString())
            .ConfigureServices(services => { services.RemoveAll<IHostedService>(); services.AddDataProtection().UseEphemeralDataProtectionProvider(); }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var login = await client.GetStringAsync("/Account/Login");
        var token = Regex.Match(login, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        if (disabled)
        {
            using var signedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["Input.Username"] = admin.LoginName, ["Input.Password"] = "synthetic-shell-password", ["__RequestVerificationToken"] = token }));
            Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
            var identity = await client.GetStringAsync($"/Admin/Events/Identity/{item.Id}");
            token = Regex.Match(identity, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
            await using var db = new ApplicationDbContext(options);
            (await db.Accounts.SingleAsync()).Disable(Now, null, "Controlled fixture");
            await db.SaveChangesAsync();
        }
        using var request = new HttpRequestMessage(post ? HttpMethod.Post : HttpMethod.Get,
            $"/Admin/Events/Identity/{item.Id}" + (post ? "" : "?handler=Current"));
        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        if (post) request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        { ["__RequestVerificationToken"] = token, ["Input.Name"] = "Must not save", ["Input.Timezone"] = "UTC" });
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
        if (disabled) Assert.Contains("accessChanged=true", response.Headers.Location!.ToString());
        Assert.Equal("", await response.Content.ReadAsStringAsync());
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(item.Name, (await verify.Events.SingleAsync()).Name);
        Assert.False(await verify.AuditEntries.AnyAsync(entry => entry.Action == "event.identity_updated"));
    }
}
