using System.Globalization;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Infrastructure.WiseOldMan;

namespace Bingo.IntegrationTests;

public sealed class WiseOldManAccountValidationTests
{
    [Fact]
    public async Task SuccessfulResultsAreCachedByNormalizedNameAndExpireAfterFiveMinutes()
    {
        var clock = new MutableClock(DateTimeOffset.Parse("2026-09-22T10:00:00Z", CultureInfo.InvariantCulture));
        var lookup = new RecordingLookup(_ => new(WiseOldManLookupStatus.Success, 10m, clock.GetUtcNow()));
        var validator = new WiseOldManAccountValidation(lookup, clock);
        var request = Request([" Alice "]);

        Assert.Equal(WiseOldManAccountValidationOutcome.Success, (await validator.ValidateAsync(request)).Outcome);
        Assert.Equal(WiseOldManAccountValidationOutcome.Success, (await validator.ValidateAsync(request with { CharacterNames = ["ALICE"] })).Outcome);
        Assert.Equal(WiseOldManAccountValidationOutcome.Success, (await validator.ValidateAsync(request with { CharacterNames = ["Bob"] })).Outcome);
        Assert.Equal(2, lookup.Calls);

        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(WiseOldManAccountValidationOutcome.Success, (await validator.ValidateAsync(request)).Outcome);
        Assert.Equal(3, lookup.Calls);
    }

    [Fact]
    public async Task MissingAndOperationalFailuresCannotBeConfirmedTogether()
    {
        var lookup = new RecordingLookup(name => name switch
        {
            "MISSING" => new(WiseOldManLookupStatus.NotFound),
            _ => new(WiseOldManLookupStatus.Unavailable)
        });
        var validator = new WiseOldManAccountValidation(lookup, TimeProvider.System);

        var result = await validator.ValidateAsync(Request(["Missing", "Outage"], enabledAdmin: true));

        Assert.Equal(WiseOldManAccountValidationOutcome.KnownInvalid, result.Outcome);
        Assert.True(result.HasKnownInvalid);
        Assert.Null(result.ConfirmationToken);
    }

    [Fact]
    public async Task AdminConfirmationIsBoundToActorActionEventParticipantNamesAndVersion()
    {
        var lookup = new RecordingLookup(_ => new(WiseOldManLookupStatus.Unavailable));
        var validator = new WiseOldManAccountValidation(lookup, TimeProvider.System);
        var request = Request(["Alice"], enabledAdmin: true) with
        {
            Action = "participant.edit",
            EventId = Guid.NewGuid(),
            ParticipantId = Guid.NewGuid(),
            ExpectedVersion = 4
        };

        var confirmation = await validator.ValidateAsync(request);
        Assert.Equal(WiseOldManAccountValidationOutcome.ConfirmationRequired, confirmation.Outcome);
        Assert.NotNull(confirmation.ConfirmationToken);

        var forged = await validator.ValidateAsync(request with
        {
            CharacterNames = ["Bob"],
            ConfirmationToken = confirmation.ConfirmationToken
        });
        Assert.Equal(WiseOldManAccountValidationOutcome.ConfirmationRequired, forged.Outcome);
        Assert.NotNull(forged.ConfirmationToken);

        var accepted = await validator.ValidateAsync(request with { ConfirmationToken = confirmation.ConfirmationToken });
        Assert.Equal(WiseOldManAccountValidationOutcome.ConfirmedOperationalFailure, accepted.Outcome);
        Assert.True(accepted.CanProceed);

        var replay = await validator.ValidateAsync(request with { ConfirmationToken = confirmation.ConfirmationToken });
        Assert.Equal(WiseOldManAccountValidationOutcome.ConfirmationRequired, replay.Outcome);
    }

    [Fact]
    public async Task ManualEntryAndReplacementConfirmationsAreBoundToTheirTarget()
    {
        var lookup = new RecordingLookup(_ => new(WiseOldManLookupStatus.Unavailable));
        var validator = new WiseOldManAccountValidation(lookup, TimeProvider.System);
        var teamRequest = Request(["Alice"], enabledAdmin: true) with { Action = "participant.manual-preformed", TargetTeamId = Guid.NewGuid() };
        var teamConfirmation = await validator.ValidateAsync(teamRequest);
        Assert.Equal(WiseOldManAccountValidationOutcome.ConfirmationRequired, teamConfirmation.Outcome);

        var otherTeam = await validator.ValidateAsync(teamRequest with
        {
            TargetTeamId = Guid.NewGuid(),
            ConfirmationToken = teamConfirmation.ConfirmationToken
        });
        Assert.Equal(WiseOldManAccountValidationOutcome.ConfirmationRequired, otherTeam.Outcome);
        Assert.Equal(WiseOldManAccountValidationOutcome.ConfirmedOperationalFailure,
            (await validator.ValidateAsync(teamRequest with { ConfirmationToken = teamConfirmation.ConfirmationToken })).Outcome);

        var vacancyRequest = Request(["Alice"], enabledAdmin: true) with
        {
            Action = "participant.replacement",
            TargetVacancyId = Guid.NewGuid()
        };
        var vacancyConfirmation = await validator.ValidateAsync(vacancyRequest);
        Assert.Equal(WiseOldManAccountValidationOutcome.ConfirmationRequired, vacancyConfirmation.Outcome);

        var otherVacancy = await validator.ValidateAsync(vacancyRequest with
        {
            TargetVacancyId = Guid.NewGuid(),
            ConfirmationToken = vacancyConfirmation.ConfirmationToken
        });
        Assert.Equal(WiseOldManAccountValidationOutcome.ConfirmationRequired, otherVacancy.Outcome);
        Assert.Equal(WiseOldManAccountValidationOutcome.ConfirmedOperationalFailure,
            (await validator.ValidateAsync(vacancyRequest with { ConfirmationToken = vacancyConfirmation.ConfirmationToken })).Outcome);
    }

    private static WiseOldManAccountValidationRequest Request(IReadOnlyCollection<string> names, bool enabledAdmin = false) =>
        new(Guid.NewGuid(), "signup.create", Guid.NewGuid(), null, 1, names, enabledAdmin);

    private sealed class RecordingLookup(Func<string, WiseOldManPlayerLookupResult> resultFactory) : IWiseOldManPlayerLookup
    {
        public int Calls { get; private set; }
        public Task<WiseOldManPlayerLookupResult> LookupPlayerAsync(string characterName, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(resultFactory(characterName));
        }
    }

    private sealed class MutableClock(DateTimeOffset current) : TimeProvider
    {
        private DateTimeOffset current = current;
        public override DateTimeOffset GetUtcNow() => current;
        public void Advance(TimeSpan amount) => current = current.Add(amount);
    }
}
