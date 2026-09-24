using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Bingo.Application.Integrations.WiseOldMan;

namespace Bingo.Infrastructure.WiseOldMan;

/// <summary>
/// Validates the exact name set submitted to an account-bearing write. The
/// player client owns the five-minute successful-result cache and limiter;
/// this adapter only classifies the per-name results and binds an Admin
/// operational-failure acknowledgement to the mutation context.
/// </summary>
public sealed class WiseOldManAccountValidation(
    IWiseOldManPlayerLookup playerLookup,
    TimeProvider timeProvider) : IWiseOldManAccountValidation
{
    private static readonly TimeSpan ConfirmationLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan SuccessLifetime = TimeSpan.FromMinutes(5);
    private readonly ConcurrentDictionary<string, PendingConfirmation> confirmations = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> successfulNames = new(StringComparer.Ordinal);

    public void RememberSuccessfulLookup(string characterName, DateTimeOffset fetchedAt)
    {
        var normalized = Normalize(characterName);
        if (!string.IsNullOrWhiteSpace(normalized)) successfulNames[normalized] = fetchedAt;
    }

    public async Task<WiseOldManAccountValidationResult> ValidateAsync(
        WiseOldManAccountValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        var names = request.CharacterNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => (Display: name.Trim(), Normalized: Normalize(name)))
            .DistinctBy(name => name.Normalized, StringComparer.Ordinal)
            .OrderBy(name => name.Normalized, StringComparer.Ordinal)
            .ToList();
        if (names.Count == 0)
            return new(WiseOldManAccountValidationOutcome.Success, []);

        var issues = new List<WiseOldManAccountValidationIssue>();
        foreach (var name in names)
        {
            if (successfulNames.TryGetValue(name.Normalized, out var successfulAt) && timeProvider.GetUtcNow() - successfulAt < SuccessLifetime)
                continue;
            var result = await playerLookup.LookupPlayerAsync(name.Normalized, cancellationToken);
            if (result.Status == WiseOldManLookupStatus.Success)
                successfulNames[name.Normalized] = result.FetchedAt ?? timeProvider.GetUtcNow();
            else
                issues.Add(new(name.Display, name.Normalized, result.Status, result.RetryAt));
        }

        if (issues.Count == 0)
            return new(WiseOldManAccountValidationOutcome.Success, []);

        if (issues.Any(issue => issue.Status == WiseOldManLookupStatus.NotFound))
            return new(WiseOldManAccountValidationOutcome.KnownInvalid, issues);

        if (!request.EnabledAdmin)
            return new(WiseOldManAccountValidationOutcome.OperationalFailure, issues);

        var fingerprint = Fingerprint(request, names.Select(item => item.Normalized));
        if (!string.IsNullOrWhiteSpace(request.ConfirmationToken) &&
            confirmations.TryGetValue(request.ConfirmationToken, out var pending))
        {
            if (pending.ExpiresAt < timeProvider.GetUtcNow())
                confirmations.TryRemove(request.ConfirmationToken, out _);
            else if (string.Equals(pending.Fingerprint, fingerprint, StringComparison.Ordinal) &&
                     confirmations.TryRemove(request.ConfirmationToken, out _))
                return new(WiseOldManAccountValidationOutcome.ConfirmedOperationalFailure, issues);
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        confirmations[token] = new(fingerprint, timeProvider.GetUtcNow().Add(ConfirmationLifetime));
        return new(WiseOldManAccountValidationOutcome.ConfirmationRequired, issues, token);
    }

    private static string Normalize(string name) => name.Trim().ToUpperInvariant();

    private static string Fingerprint(WiseOldManAccountValidationRequest request, IEnumerable<string> names)
    {
        var value = string.Join('|',
            request.ActorAccountId.ToString("N"),
            request.Action,
            request.EventId?.ToString("N") ?? string.Empty,
            request.ParticipantId?.ToString("N") ?? string.Empty,
            request.ExpectedVersion?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            request.TargetTeamId?.ToString("N") ?? string.Empty,
            request.TargetVacancyId?.ToString("N") ?? string.Empty,
            string.Join(',', names));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private sealed record PendingConfirmation(string Fingerprint, DateTimeOffset ExpiresAt);
}
