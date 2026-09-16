using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bingo.Application.Integrations.WiseOldMan;
using Microsoft.Extensions.Logging;

namespace Bingo.Infrastructure.WiseOldMan;

public sealed class WiseOldManClient(
    IHttpClientFactory httpClientFactory,
    WiseOldManRequestLimiter limiter,
    TimeProvider time,
    ILogger<WiseOldManClient> logger) : IWiseOldManPlayerLookup, IWiseOldManCompetitionClient, IWiseOldManStatus
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private static readonly Action<ILogger, string, DateTimeOffset, Exception?> LogSuccess =
        LoggerMessage.Define<string, DateTimeOffset>(LogLevel.Information, new EventId(10102), "Wise Old Man player lookup succeeded for {CharacterName} at {FetchedAt}");
    private static readonly Action<ILogger, Exception?> LogUnexpectedFailure =
        LoggerMessage.Define(LogLevel.Warning, new EventId(10103), "Wise Old Man request failed unexpectedly.");
    private readonly ConcurrentDictionary<string, CachedPlayer> cache = new(StringComparer.Ordinal);

    public WiseOldManRequestStatus GetStatus() => limiter.GetStatus();

    public async Task<WiseOldManPlayerLookupResult> LookupPlayerAsync(string characterName, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(characterName);
        if (string.IsNullOrWhiteSpace(normalized)) return new(WiseOldManLookupStatus.NotFound, Message: "An OSRS character name is required.");
        var now = time.GetUtcNow();
        if (cache.TryGetValue(normalized, out var cached) && now - cached.FetchedAt < CacheTtl)
            return new(WiseOldManLookupStatus.Success, cached.Ehb, cached.FetchedAt);

        await using var admission = await limiter.AdmitAsync(cancellationToken);
        if (!admission.AllowedRequest)
            return new(WiseOldManLookupStatus.RateLimited, RetryAt: admission.RetryAt, Message: "Wise Old Man is temporarily busy. Try again in about 1 minute.");

        HttpResponseMessage? response = null;
        var validPayload = false;
        try
        {
            var client = httpClientFactory.CreateClient("WiseOldMan");
            response = await client.GetAsync($"players/{Uri.EscapeDataString(characterName.Trim())}", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                await admission.CompleteAsync(response, true, false, cancellationToken);
                return new(WiseOldManLookupStatus.NotFound, Message: "Wise Old Man could not find that character.");
            }
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                await admission.CompleteAsync(response, true, false, cancellationToken);
                return new(WiseOldManLookupStatus.RateLimited, RetryAt: limiter.GetStatus().NextPermittedAt, Message: "Wise Old Man is temporarily busy. Try again in about 1 minute.");
            }
            if (!response.IsSuccessStatusCode)
            {
                await admission.CompleteAsync(response, true, false, cancellationToken);
                return new(WiseOldManLookupStatus.Unavailable, RetryAt: NextRetryAt(), Message: "Wise Old Man is unavailable right now. Your current EHB was kept.");
            }
            var payload = await response.Content.ReadFromJsonAsync<PlayerPayload>(cancellationToken: cancellationToken);
            if (payload?.Ehb is null || payload.Ehb < 0)
            {
                await admission.CompleteAsync(response, false, false, cancellationToken);
                return new(WiseOldManLookupStatus.Unavailable, Message: "Wise Old Man returned an unreadable EHB value. Your current EHB was kept.");
            }
            validPayload = true;
            var fetchedAt = time.GetUtcNow();
            cache[normalized] = new(payload.Ehb.Value, fetchedAt);
            await admission.CompleteAsync(response, true, false, cancellationToken);
            LogSuccess(logger, characterName.Trim(), fetchedAt, null);
            return new(WiseOldManLookupStatus.Success, payload.Ehb.Value, fetchedAt);
        }
        catch (OperationCanceledException)
        {
            await admission.CompleteAsync(response, validPayload, true, CancellationToken.None);
            if (cancellationToken.IsCancellationRequested) throw;
            return new(WiseOldManLookupStatus.Unavailable, RetryAt: limiter.GetStatus().NextPermittedAt, Message: "Wise Old Man timed out. Your current EHB was kept.");
        }
        catch (HttpRequestException)
        {
            await admission.CompleteAsync(response, validPayload, true, CancellationToken.None);
            return new(WiseOldManLookupStatus.Unavailable, RetryAt: limiter.GetStatus().NextPermittedAt, Message: "Wise Old Man is unavailable right now. Your current EHB was kept.");
        }
        catch (JsonException)
        {
            await admission.CompleteAsync(response, false, false, CancellationToken.None);
            return new(WiseOldManLookupStatus.Unavailable, RetryAt: limiter.GetStatus().NextPermittedAt, Message: "Wise Old Man returned an unreadable response. Your current EHB was kept.");
        }
        catch (Exception exception)
        {
            await admission.CompleteAsync(response, false, true, CancellationToken.None);
            LogUnexpectedFailure(logger, exception);
            return new(WiseOldManLookupStatus.Unavailable, RetryAt: limiter.GetStatus().NextPermittedAt, Message: "Wise Old Man is unavailable right now. Your current EHB was kept.");
        }
        finally { response?.Dispose(); }
    }

    public Task<WiseOldManCompetitionResult> GetCompetitionAsync(long competitionId, CancellationToken cancellationToken = default) =>
        GetCompetitionAsync(competitionId, [], cancellationToken);

    public async Task<WiseOldManCompetitionResult> GetCompetitionAsync(long competitionId, IReadOnlyCollection<string> metrics, CancellationToken cancellationToken = default)
    {
        if (competitionId <= 0) return new(WiseOldManCompetitionStatus.Invalid, Message: "A positive Wise Old Man competition ID is required.");
        await using var admission = await limiter.AdmitAsync(cancellationToken);
        if (!admission.AllowedRequest)
            return new(WiseOldManCompetitionStatus.RateLimited, RetryAt: admission.RetryAt, Message: "Wise Old Man is temporarily busy. Try again in about 1 minute.");

        HttpResponseMessage? response = null;
        try
        {
            var client = httpClientFactory.CreateClient("WiseOldMan");
            var requested = metrics.Append("ehb").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var query = string.Join('&', requested.Select(metric => $"metrics={Uri.EscapeDataString(metric)}"));
            response = await client.GetAsync($"competitions/{competitionId}?{query}", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                await admission.CompleteAsync(response, true, false, cancellationToken);
                return new(WiseOldManCompetitionStatus.NotFound, Message: "Wise Old Man could not find that competition.");
            }
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                await admission.CompleteAsync(response, true, false, cancellationToken);
                return new(WiseOldManCompetitionStatus.RateLimited, RetryAt: limiter.GetStatus().NextPermittedAt, Message: "Wise Old Man is temporarily busy. Try again in about 1 minute.");
            }
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden)
            {
                await admission.CompleteAsync(response, true, false, cancellationToken);
                return new(WiseOldManCompetitionStatus.Invalid, Message: "Wise Old Man did not allow access to that competition.");
            }
            if (!response.IsSuccessStatusCode)
            {
                await admission.CompleteAsync(response, true, false, cancellationToken);
                return new(WiseOldManCompetitionStatus.Unavailable, RetryAt: NextRetryAt(), Message: "Wise Old Man is unavailable right now.");
            }

            var payload = await response.Content.ReadFromJsonAsync<CompetitionPayload>(cancellationToken: cancellationToken);
            if (payload is null || payload.StartsAt is null || payload.EndsAt is null || payload.EndsAt <= payload.StartsAt || payload.Participations is null)
            {
                await admission.CompleteAsync(response, false, false, cancellationToken);
                return new(WiseOldManCompetitionStatus.Invalid, Message: "Wise Old Man returned incomplete competition details.");
            }

            await admission.CompleteAsync(response, true, false, cancellationToken);
            var participants = payload.Participations
                .Where(item => !string.IsNullOrWhiteSpace(item.Player?.Username))
                .Select(item =>
                {
                    // Parse each metric independently: a malformed boss value must not erase valid EHB.
                    // Neither heterogeneous totals nor deprecated progress fields are activity evidence.
                    var deltas = ParseDeltas(item.Deltas, requested);
                    var ehb = deltas.GetValueOrDefault("ehb");
                    return new WiseOldManCompetitionParticipant(
                        item.Player!.Username!.Trim(), item.Player.Type, ehb?.Gained, ehb?.Start, ehb?.End, deltas, item.Player.UpdatedAt?.ToUniversalTime());
                })
                .ToList();
            return new(WiseOldManCompetitionStatus.Success,
                new WiseOldManCompetition(payload.Id ?? competitionId, payload.Title?.Trim() ?? $"Competition {competitionId}",
                    payload.StartsAt.Value.ToUniversalTime(), payload.EndsAt.Value.ToUniversalTime(), payload.UpdatedAt?.ToUniversalTime(), participants));
        }
        catch (OperationCanceledException)
        {
            await admission.CompleteAsync(response, false, true, CancellationToken.None);
            if (cancellationToken.IsCancellationRequested) throw;
            return new(WiseOldManCompetitionStatus.Unavailable, RetryAt: limiter.GetStatus().NextPermittedAt, Message: "Wise Old Man timed out.");
        }
        catch (HttpRequestException)
        {
            await admission.CompleteAsync(response, false, true, CancellationToken.None);
            return new(WiseOldManCompetitionStatus.Unavailable, RetryAt: limiter.GetStatus().NextPermittedAt, Message: "Wise Old Man is unavailable right now.");
        }
        catch (JsonException)
        {
            await admission.CompleteAsync(response, false, false, CancellationToken.None);
            return new(WiseOldManCompetitionStatus.Invalid, RetryAt: limiter.GetStatus().NextPermittedAt, Message: "Wise Old Man returned an unreadable competition response.");
        }
        catch (Exception exception)
        {
            await admission.CompleteAsync(response, false, true, CancellationToken.None);
            LogUnexpectedFailure(logger, exception);
            return new(WiseOldManCompetitionStatus.Unavailable, RetryAt: limiter.GetStatus().NextPermittedAt, Message: "Wise Old Man is unavailable right now.");
        }
        finally { response?.Dispose(); }
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
    private DateTimeOffset? NextRetryAt()
    {
        var status = limiter.GetStatus();
        return status.NextPermittedAt ?? status.ResetAt;
    }

    private sealed record CachedPlayer(decimal Ehb, DateTimeOffset FetchedAt);
    private sealed record PlayerPayload(decimal? Ehb);
    private sealed record CompetitionPayload(
        long? Id,
        string? Title,
        DateTimeOffset? StartsAt,
        DateTimeOffset? EndsAt,
        DateTimeOffset? UpdatedAt,
        List<CompetitionParticipationPayload>? Participations);
    private sealed record CompetitionParticipationPayload(CompetitionPlayerPayload? Player, JsonElement Deltas);
    private sealed record CompetitionPlayerPayload(string? Username, string? Type, DateTimeOffset? UpdatedAt);

    private static Dictionary<string, WiseOldManMetricDelta> ParseDeltas(JsonElement payload, IReadOnlyCollection<string> requested)
    {
        var result = new Dictionary<string, WiseOldManMetricDelta>(StringComparer.Ordinal);
        if (payload.ValueKind != JsonValueKind.Array) return result;
        foreach (var entry in payload.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("metric", out var name) || name.ValueKind != JsonValueKind.String ||
                name.GetString() is not { } metric) continue;
            if (string.Equals(metric, "ehb", StringComparison.OrdinalIgnoreCase)) metric = "ehb";
            if (!requested.Contains(metric, StringComparer.Ordinal)) continue;
            var delta = entry.TryGetProperty("values", out var values) ? ParseDelta(values) : new WiseOldManMetricDelta(null, null, null);
            if (!result.TryAdd(metric, delta)) result[metric] = new(null, null, null);
        }
        return result;
    }

    private static WiseOldManMetricDelta ParseDelta(JsonElement values) => new(ReadNumber(values, "start"), ReadNumber(values, "end"), ReadNumber(values, "gained"));

    private static decimal? ReadNumber(JsonElement values, string name) =>
        values.ValueKind == JsonValueKind.Object && values.TryGetProperty(name, out var number) && number.ValueKind == JsonValueKind.Number && number.TryGetDecimal(out var value) ? value : null;
}
