using System.Data;
using System.Text.Json;
using Bingo.Application.Events;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bingo.Infrastructure.Events;

public sealed class EventCreationService(ApplicationDbContext db, TimeProvider time) : IEventCreationService
{
    public async Task<EventCreationResult> CreateAsync(Guid requestId, string? name, string? timezone, LifecycleActor actor, CancellationToken ct = default)
    {
        name = name?.Trim() ?? string.Empty;
        timezone = timezone?.Trim() ?? string.Empty;
        if (requestId == Guid.Empty) return Invalid("Reload the page before creating an event.");
        if (string.IsNullOrWhiteSpace(name)) return Invalid("Enter an event name.", "Name");
        if (WiseOldManCompetitionRules.ProviderCharacterCount(name) > WiseOldManCompetitionRules.MaximumCompetitionTitleLength)
            return Invalid("Event names must be 50 characters or fewer.", "Name");
        if (timezone is not ("Europe/Copenhagen" or "UTC") || !TimeZoneInfo.TryFindSystemTimeZoneById(timezone, out _))
            return Invalid("Choose a supported timezone.", "Timezone");

        for (var sequence = 1; ; sequence++)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            await LockAsync(actor.Id, requestId, ct);
            var account = await AuthorizedAccountAsync(actor.Id, ct);
            if (account is null) return new(EventCreationOutcome.Forbidden);
            var previous = await db.EventCreationOperations.AsNoTracking()
                .SingleOrDefaultAsync(x => x.ActorAccountId == actor.Id && x.RequestId == requestId, ct);
            if (previous is not null)
            {
                if (previous.Name != name || previous.Timezone != timezone)
                    return new(EventCreationOutcome.Conflict, Error: "This creation request already completed with different values. Start a new request to create another event.");
                return await ResultAsync(previous, account.GlobalRole == GlobalRole.SuperAdmin, ct);
            }

            var now = time.GetUtcNow();
            var item = new BingoEvent(Guid.NewGuid(), name, EventSlugGenerator.GenerateCandidate(name, sequence), timezone, actor.Id, now);
            var form = new SignupForm(Guid.NewGuid(), item.Id, now);
            var board = new Board(Guid.NewGuid(), item.Id, "Main board", 5, 5);
            item.ConfigureSignup(waitingListEnabled: true, requireCode: false, codeHash: null);
            var audit = new AuditEntry(Guid.NewGuid(), now, actor.Id, actor.Username, "event.created", "event",
                item.Id.ToString(), "Created as a private draft.", item.Id, null, JsonSerializer.Serialize(AuditState(item)));
            try
            {
                db.Events.Add(item);
                db.SignupForms.Add(form);
                db.SignupQuestions.AddRange(DefaultQuestions(form.Id, item.Id));
                db.Boards.Add(board);
                db.AuditEntries.Add(audit);
                db.EventCreationOperations.Add(new(actor.Id, requestId, name, timezone, item.Id));
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return new(EventCreationOutcome.Completed, item.Id, name);
            }
            catch (DbUpdateException exception) when (IsSlugCollision(exception))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
            }
            catch
            {
                // Disposal rolls back an uncommitted aggregate. A lost commit response
                // leaves the durable operation available to the same-key retry.
                db.ChangeTracker.Clear();
                throw;
            }
        }
    }

    public async Task<EventCreationResult> CheckAgainAsync(Guid requestId, LifecycleActor actor, CancellationToken ct = default)
    {
        if (requestId == Guid.Empty) return Invalid("Supply a valid creation request key.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await LockAsync(actor.Id, requestId, ct);
        var account = await AuthorizedAccountAsync(actor.Id, ct);
        if (account is null) return new(EventCreationOutcome.Forbidden);
        var operation = await db.EventCreationOperations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ActorAccountId == actor.Id && x.RequestId == requestId, ct);
        return operation is null ? new(EventCreationOutcome.NotFound) : await ResultAsync(operation, account.GlobalRole == GlobalRole.SuperAdmin, ct);
    }

    private Task<Account?> AuthorizedAccountAsync(Guid actorId, CancellationToken ct) => db.Accounts.AsNoTracking()
        .SingleOrDefaultAsync(x => x.Id == actorId && x.Active && x.AccountType == AccountType.WebsiteAccount
            && (x.GlobalRole == GlobalRole.Admin || x.GlobalRole == GlobalRole.SuperAdmin), ct);

    private async Task<EventCreationResult> ResultAsync(EventCreationOperation operation, bool superAdmin, CancellationToken ct)
    {
        var visible = await db.Events.AsNoTracking().AnyAsync(x => x.Id == operation.EventId
            && x.State != EventState.Discarded && (x.HiddenAt == null || superAdmin), ct);
        return visible ? new(EventCreationOutcome.Completed, operation.EventId, operation.Name) : new(EventCreationOutcome.NotFound);
    }

    private Task<int> LockAsync(Guid actorId, Guid requestId, CancellationToken ct)
    {
        var identity = $"event-create:{actorId:N}:{requestId:N}";
        return db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({identity}, 0))", ct);
    }

    private static EventCreationResult Invalid(string error, string? field = null) => new(EventCreationOutcome.ValidationFailed, Error: error, Field: field);

    private static IReadOnlyList<SignupQuestion> DefaultQuestions(Guid formId, Guid eventId) =>
    [
        new SignupQuestion(Guid.NewGuid(), formId, eventId, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing),
        new SignupQuestion(Guid.NewGuid(), formId, eventId, "captain_volunteer", "Captain volunteer", SignupQuestionType.YesNo, true, 1, null, SignupSystemField.CaptainVolunteer),
        new SignupQuestion(Guid.NewGuid(), formId, eventId, SignupQuestion.CoCaptainKey, SignupQuestion.CoCaptainLabel, SignupQuestionType.Text, false, 2, null, SignupSystemField.CoCaptainName)
    ];

    private static object AuditState(BingoEvent item) => new
    {
        item.Name,
        item.Slug,
        Description = AuditDescription(item.Description),
        item.Timezone,
        item.State
    };

    private static string? AuditDescription(string? description) => description is null
        ? null
        : description.Length <= 500 ? description : $"{description[..500]}…";

    private static bool IsSlugCollision(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_events_slug"
        };

}
