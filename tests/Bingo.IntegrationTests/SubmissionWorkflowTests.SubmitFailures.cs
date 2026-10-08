using System.Globalization;
using System.Security.Claims;
using Bingo.Infrastructure.Evidence;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bingo.IntegrationTests;

public sealed partial class SubmissionWorkflowTests
{
    // Bug report (8 Oct): the submit drawer hid the "no active evidence code"
    // refusal behind the generic failure and logged it as unexpected (4101).
    [Theory]
    [InlineData("en", "Evidence codes are enabled, but no code is active at the current time. Ask an administrator to activate one before submitting.")]
    [InlineData("da", "Dokumentationskoder er slået til, men ingen kode er aktiv lige nu. Bed en administrator om at aktivere en, før du indsender.")]
    public async Task SubmitDrawerShowsTheMissingEvidenceCodeRefusalInTheRequestLanguage(string culture, string expected)
    {
        var setup = await SeedAsync(3, true, evidenceCode: string.Empty);
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        try
        {
            await using var db = new ApplicationDbContext(options);
            var logger = new RecordingLogger<Bingo.Web.Pages.Captain.SubmitModel>();
            var page = DrawerPage(db, setup.CaptainId, logger);
            page.Input = new Bingo.Web.Pages.Captain.SubmitModel.SubmissionInput
            {
                TileId = setup.TileId, RequirementId = setup.RequirementId, DropSnapshotId = setup.DropId,
                CreditedParticipantId = setup.ParticipantId, ClaimedWeight = 1,
                Evidence = new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "Input.Evidence", "proof.png")
            };

            Assert.IsType<PartialViewResult>(await page.OnPostDrawerAsync(setup.TileId, setup.EventId, setup.TeamId, CancellationToken.None));

            var error = Assert.Single(page.ModelState[string.Empty]!.Errors);
            Assert.Equal(expected, error.ErrorMessage);
            Assert.DoesNotContain(logger.Levels, level => level >= LogLevel.Error);
            Assert.False(await db.Submissions.AnyAsync(x => x.EventId == setup.EventId));
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    private Bingo.Web.Pages.Captain.SubmitModel DrawerPage(ApplicationDbContext db, Guid actorAccountId, ILogger<Bingo.Web.Pages.Captain.SubmitModel> logger) =>
        new(db, Service(db), new EvidenceAuthority(db), new FixedTimeProvider(now), SharedResourceLocalizer(), logger)
        {
            MetadataProvider = new EmptyModelMetadataProvider(),
            PageContext = new PageContext
            {
                ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()),
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actorAccountId.ToString())], "test"))
                }
            }
        };

    private static StringLocalizer<Bingo.Web.SharedResource> SharedResourceLocalizer() =>
        new StringLocalizer<Bingo.Web.SharedResource>(new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions { ResourcesPath = "Resources" }), NullLoggerFactory.Instance));

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogLevel> Levels { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Levels.Add(logLevel);
    }
}
