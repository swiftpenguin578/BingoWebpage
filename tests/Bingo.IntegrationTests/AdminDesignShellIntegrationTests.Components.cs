using System.Text.RegularExpressions;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bingo.IntegrationTests;

public sealed partial class AdminDesignShellIntegrationTests
{
    [Fact]
    public async Task SharedReferenceComponentsRenderFromSamePartials()
    {
        var admin = Admin(); var item = Event(admin, EventState.Draft, "Component fixture", 2);
        await using (var db = new ApplicationDbContext(options)) { db.AddRange(admin, item); await db.SaveChangesAsync(); }
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Testing").UseSetting("ConnectionStrings:Database", database.GetConnectionString())
            .ConfigureServices(services => { services.RemoveAll<IHostedService>(); services.AddDataProtection().UseEphemeralDataProtectionProvider(); }));
        using var client = await IdentityClientAsync(factory);
        var html = await client.GetStringAsync($"/Admin/Events/Identity/{item.Id}");
        var templates = string.Join('\n', Regex.Matches(html, "<template data-admin-template=\"[^\"]+\">.*?</template>", RegexOptions.Singleline).Select(match => match.Value));
        Assert.Equal(11, Regex.Count(templates, "<template "));
        Assert.Contains("data-icon=\"check\"", templates);
        Assert.Contains("data-icon=\"error\"", templates);
        Assert.Contains("class=\"m-body\"", templates);
        Assert.DoesNotContain("m-sub", templates);
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Bingo.slnx"))) directory = directory.Parent;
        var path = Path.Combine(Assert.IsType<DirectoryInfo>(directory).FullName, "tests/Bingo.BrowserTests/fixtures/admin-design-templates.html");
        if (Environment.GetEnvironmentVariable("BINGO_EXPORT_ADMIN_TEMPLATES") == "1") await File.WriteAllTextAsync(path, templates + "\n");
        Assert.Equal(templates, (await File.ReadAllTextAsync(path)).TrimEnd());
    }
}
