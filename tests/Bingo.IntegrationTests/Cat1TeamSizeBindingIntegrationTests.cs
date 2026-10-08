using System.Net;
using Bingo.Domain.Access;
using Bingo.Infrastructure.Persistence;
using Bingo.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class Slice6CatalogueAdministrationIntegrationTests
{
    [Fact]
    public async Task Cat1TeamSizeHttpBindingRejectsInvalidValuesWithoutMutationForOrdinaryAdmin()
    {
        var (actor, boss, _, _) = await PriceFixtureAsync();
        await using (var role = new ApplicationDbContext(options))
        {
            var account = role.Accounts.Single(value => value.Id == actor.Id);
            account.SetGlobalRole(GlobalRole.Admin);
            SetPassword(account, DateTimeOffset.UtcNow);
            await role.SaveChangesAsync();
        }

        await using (var setup = new ApplicationDbContext(options))
        {
            var current = await setup.BossActivities.SingleAsync(value => value.Id == boss.Id);
            current.SetTeamSize(5);
            await setup.SaveChangesAsync();
        }

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, actor.LoginName);

        var updatePage = await client.GetStringAsync($"/Admin/Catalogue?bossId={boss.Id}");
        await using (var beforeOmitted = new ApplicationDbContext(options))
        {
            var current = await beforeOmitted.BossActivities.SingleAsync(value => value.Id == boss.Id);
            var fields = new Dictionary<string, string>
            {
                ["recordId"] = current.Id.ToString(),
                ["expectedVersion"] = current.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["name"] = "Omitted team size activity",
                ["category"] = current.Category,
                ["efficientRate"] = current.EfficientCompletionsPerHour!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };
            using var response = await PostAsync(client, $"/Admin/Catalogue?handler=UpdateBoss&bossId={boss.Id}", updatePage, fields);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        await using (var afterOmitted = new ApplicationDbContext(options))
        {
            var current = await afterOmitted.BossActivities.SingleAsync(value => value.Id == boss.Id);
            Assert.Equal("Omitted team size activity", current.Name);
            Assert.Equal(5, current.TeamSize);
        }

        foreach (var invalid in new[] { "abc", "2.5", string.Empty, "0" })
        {
            updatePage = await client.GetStringAsync($"/Admin/Catalogue?bossId={boss.Id}");
            await using (var beforeInvalid = new ApplicationDbContext(options))
            {
                var current = await beforeInvalid.BossActivities.SingleAsync(value => value.Id == boss.Id);
                var fields = new Dictionary<string, string>
                {
                    ["recordId"] = current.Id.ToString(),
                    ["expectedVersion"] = current.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["name"] = $"Invalid team size {invalid}-{Guid.NewGuid():N}",
                    ["category"] = current.Category,
                    ["efficientRate"] = current.EfficientCompletionsPerHour!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["teamSize"] = invalid
                };
                using var response = await PostAsync(client, $"/Admin/Catalogue?handler=UpdateBoss&bossId={boss.Id}", updatePage, fields);
                Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            }

            await using var afterInvalid = new ApplicationDbContext(options);
            var unchanged = await afterInvalid.BossActivities.SingleAsync(value => value.Id == boss.Id);
            Assert.Equal("Omitted team size activity", unchanged.Name);
            Assert.Equal(5, unchanged.TeamSize);
        }

        foreach (var invalid in new[] { "abc", "2.5", string.Empty, "0" })
        {
            var name = $"Rejected add {invalid}-{Guid.NewGuid():N}";
            var addPage = await client.GetStringAsync("/Admin/Catalogue?addBoss=true");
            var fields = new Dictionary<string, string>
            {
                ["Boss.Name"] = name,
                ["Boss.Category"] = "Boss",
                ["Boss.EfficientRate"] = "10",
                ["Boss.TeamSize"] = invalid
            };
            using var response = await PostAsync(client, "/Admin/Catalogue?handler=Boss&addBoss=true", addPage, fields);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            await using var afterInvalidAdd = new ApplicationDbContext(options);
            Assert.False(await afterInvalidAdd.BossActivities.AnyAsync(value => value.Name == name));
        }
    }
}
