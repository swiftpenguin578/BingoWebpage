using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bingo.Application.Events;
using Bingo.Application.Integrations.WiseOldMan;

namespace Bingo.Infrastructure.WiseOldMan;

public sealed class WiseOldManCompetitionManagementClient(
    IHttpClientFactory httpClientFactory,
    WiseOldManRequestLimiter limiter,
    TimeProvider time,
    ICompetitionCredentialProtector credentialProtector) : IWiseOldManCompetitionManagementClient
{
    private static readonly JsonSerializerOptions RequestJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public Task<WiseOldManCompetitionWriteResult> CreateAsync(
        WiseOldManCompetitionWritePayload payload,
        CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Post, "competitions", payload, null, cancellationToken);

    public Task<WiseOldManCompetitionWriteResult> UpdateAsync(
        long competitionId,
        WiseOldManCompetitionWritePayload payload,
        string verificationCode,
        CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Put, $"competitions/{competitionId}", payload, verificationCode, cancellationToken);

    public Task<WiseOldManCompetitionWriteResult> DeleteAsync(
        long competitionId,
        string verificationCode,
        CancellationToken cancellationToken = default)
        => SendDeleteAsync(competitionId, verificationCode, cancellationToken);

    public async Task<WiseOldManUpdateAllResult> UpdateAllAsync(
        long competitionId,
        string verificationCode,
        DateTimeOffset dispatchDeadline,
        Func<CancellationToken, Task<bool>> recheckEligibility,
        CancellationToken cancellationToken = default)
    {
        if (competitionId <= 0 || string.IsNullOrWhiteSpace(verificationCode) || recheckEligibility is null)
            return new(WiseOldManUpdateAllStatus.Validation, ErrorCode: "InvalidUpdateAll", Message: "A competition and management credential are required.");

        await using var admission = await limiter.AdmitAsync(cancellationToken);
        if (!admission.AllowedRequest)
            return new(WiseOldManUpdateAllStatus.RateLimited, RetryAt: admission.RetryAt, ErrorCode: "RateLimited", Message: "Wise Old Man is temporarily busy.");

        if (time.GetUtcNow() > dispatchDeadline)
        {
            await admission.CompleteCachedAsync();
            return new(WiseOldManUpdateAllStatus.Validation, ErrorCode: "DispatchWindowMissed", Message: "The update-all dispatch window elapsed while waiting for Wise Old Man admission.");
        }

        bool eligible;
        try { eligible = await recheckEligibility(cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await admission.CompleteCachedAsync();
            throw;
        }
        catch
        {
            await admission.CompleteCachedAsync();
            return new(WiseOldManUpdateAllStatus.Validation, ErrorCode: "DispatchEligibilityCheckFailed", Message: "Current event and competition eligibility could not be confirmed; no update-all request was sent.");
        }
        if (!eligible)
        {
            await admission.CompleteCachedAsync();
            return new(WiseOldManUpdateAllStatus.Validation, ErrorCode: "DispatchEligibilityChanged", Message: "The event or managed competition link changed before update-all dispatch; no request was sent.");
        }
        if (time.GetUtcNow() > dispatchDeadline)
        {
            await admission.CompleteCachedAsync();
            return new(WiseOldManUpdateAllStatus.Validation, ErrorCode: "DispatchWindowMissed", Message: "The update-all dispatch window elapsed before HTTP dispatch.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"competitions/{competitionId}/update-all")
        {
            Content = JsonContent.Create(new { verificationCode }, options: RequestJsonOptions)
        };
        var client = httpClientFactory.CreateClient("WiseOldMan");
        try
        {
            if (time.GetUtcNow() > dispatchDeadline)
            {
                await admission.CompleteCachedAsync();
                return new(WiseOldManUpdateAllStatus.Validation, ErrorCode: "DispatchWindowMissed", Message: "The update-all dispatch window elapsed before HTTP dispatch.");
            }
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                await admission.CompleteAsync(response, true, false, cancellationToken);
                return new(WiseOldManUpdateAllStatus.RateLimited, RetryAt: limiter.GetStatus().NextPermittedAt, ErrorCode: "RateLimited", Message: "Wise Old Man is temporarily busy.");
            }
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                await admission.CompleteAsync(response, true, false, cancellationToken);
                return new(WiseOldManUpdateAllStatus.NotFound, ErrorCode: "NotFound", Message: "Wise Old Man could not find that competition.");
            }
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
            {
                await admission.CompleteAsync(response, true, false, cancellationToken);
                return new(WiseOldManUpdateAllStatus.Unauthorized, ErrorCode: "InvalidCredentials", Message: "Wise Old Man did not accept the competition management credentials.");
            }
            if ((int)response.StatusCode is >= 400 and < 500)
            {
                var error = await ParseErrorAsync(response, verificationCode, cancellationToken);
                await admission.CompleteAsync(response, true, false, cancellationToken);
                return new(WiseOldManUpdateAllStatus.Validation,
                    ErrorCode: error.Code ?? "ProviderValidation",
                    Message: error.Message ?? "Wise Old Man rejected the participant update request.");
            }
            if (!response.IsSuccessStatusCode)
            {
                // A failed 5xx/network response after POST has an ambiguous
                // remote outcome. Never retry it blindly.
                await admission.CompleteAsync(response, true, true, cancellationToken);
                return new(WiseOldManUpdateAllStatus.Unknown, RetryAt: NextRetryAt(), ErrorCode: "UnknownOutcome", Message: "The Wise Old Man participant update outcome is unknown.");
            }

            // The endpoint queues work asynchronously and may return no body or
            // no participant count. Any successful HTTP response is the narrow
            // provider acknowledgement we can safely persist.
            await admission.CompleteAsync(response, true, false, cancellationToken);
            return new(WiseOldManUpdateAllStatus.Acknowledged, Message: "Wise Old Man accepted the participant update request.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await admission.CompleteAsync(null, false, true, CancellationToken.None);
            return new(WiseOldManUpdateAllStatus.Unknown, RetryAt: NextRetryAt(), ErrorCode: "UnknownOutcome", Message: "The Wise Old Man participant update outcome is unknown.");
        }
        catch (HttpRequestException)
        {
            await admission.CompleteAsync(null, false, true, CancellationToken.None);
            return new(WiseOldManUpdateAllStatus.Unknown, RetryAt: NextRetryAt(), ErrorCode: "UnknownOutcome", Message: "The Wise Old Man participant update outcome is unknown.");
        }
    }

    private async Task<WiseOldManCompetitionWriteResult> SendAsync(
        HttpMethod method,
        string path,
        WiseOldManCompetitionWritePayload payload,
        string? verificationCode,
        CancellationToken cancellationToken)
    {
        if (payload.StartsAt >= payload.EndsAt)
            return new(WiseOldManCompetitionWriteStatus.Validation, ErrorCode: "InvalidSchedule", Message: "The competition schedule is invalid.");

        await using var admission = await limiter.AdmitAsync(cancellationToken);
        if (!admission.AllowedRequest)
            return new(WiseOldManCompetitionWriteStatus.RateLimited, RetryAt: admission.RetryAt, ErrorCode: "RateLimited", Message: "Wise Old Man is temporarily busy.");

        using var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(ToProviderPayload(payload, verificationCode), options: RequestJsonOptions)
        };
        try
        {
            var client = httpClientFactory.CreateClient("WiseOldMan");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                await admission.CompleteAsync(response, true, false, cancellationToken);
                return new(WiseOldManCompetitionWriteStatus.RateLimited, RetryAt: limiter.GetStatus().NextPermittedAt, ErrorCode: "RateLimited", Message: "Wise Old Man is temporarily busy.");
            }
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                await admission.CompleteAsync(response, true, false, cancellationToken);
                return new(WiseOldManCompetitionWriteStatus.NotFound, ErrorCode: "NotFound", Message: "Wise Old Man could not find that competition.");
            }
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
            {
                await admission.CompleteAsync(response, true, false, cancellationToken);
                return new(WiseOldManCompetitionWriteStatus.Unauthorized, ErrorCode: "InvalidCredentials", Message: "Wise Old Man did not accept the competition management credentials.");
            }
            if ((int)response.StatusCode is >= 400 and < 500)
            {
                var error = await ParseErrorAsync(response, verificationCode, cancellationToken);
                await admission.CompleteAsync(response, true, false, cancellationToken);
                return new(WiseOldManCompetitionWriteStatus.Validation, ErrorCode: error.Code ?? "ProviderValidation", Message: error.Message ?? "Wise Old Man rejected the competition configuration.", AffectedParticipants: error.AffectedParticipants);
            }
            if (!response.IsSuccessStatusCode)
            {
                await admission.CompleteAsync(response, true, true, cancellationToken);
                return new(WiseOldManCompetitionWriteStatus.Unknown, RetryAt: NextRetryAt(), ErrorCode: "UnknownOutcome", Message: "The Wise Old Man write outcome is unknown.");
            }

            var result = await ParseSuccessAsync(response, payload, cancellationToken);
            await admission.CompleteAsync(response, result.Competition is not null, false, cancellationToken);
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await admission.CompleteAsync(null, false, true, CancellationToken.None);
            return new(WiseOldManCompetitionWriteStatus.Unknown, RetryAt: NextRetryAt(), ErrorCode: "UnknownOutcome", Message: "The Wise Old Man write outcome is unknown.");
        }
        catch (HttpRequestException)
        {
            await admission.CompleteAsync(null, false, true, CancellationToken.None);
            return new(WiseOldManCompetitionWriteStatus.Unknown, RetryAt: NextRetryAt(), ErrorCode: "UnknownOutcome", Message: "The Wise Old Man write outcome is unknown.");
        }
        catch (JsonException)
        {
            await admission.CompleteAsync(null, false, true, CancellationToken.None);
            return new(WiseOldManCompetitionWriteStatus.Unknown, RetryAt: NextRetryAt(), ErrorCode: "MalformedSuccess", Message: "Wise Old Man returned an unreadable write response.");
        }
    }

    private async Task<WiseOldManCompetitionWriteResult> SendDeleteAsync(long competitionId, string verificationCode, CancellationToken cancellationToken)
    {
        if (competitionId <= 0 || string.IsNullOrWhiteSpace(verificationCode))
            return new(WiseOldManCompetitionWriteStatus.Validation, ErrorCode: "InvalidDelete", Message: "A competition and management credential are required.");

        await using var admission = await limiter.AdmitAsync(cancellationToken);
        if (!admission.AllowedRequest)
            return new(WiseOldManCompetitionWriteStatus.RateLimited, RetryAt: admission.RetryAt, ErrorCode: "RateLimited", Message: "Wise Old Man is temporarily busy.");

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"competitions/{competitionId}")
        {
            Content = JsonContent.Create(new { verificationCode })
        };
        try
        {
            var client = httpClientFactory.CreateClient("WiseOldMan");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                await admission.CompleteAsync(response, true, false, cancellationToken);
                return new(WiseOldManCompetitionWriteStatus.NotFound, ErrorCode: "NotFound", Message: "Wise Old Man could not find that competition.");
            }
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
            {
                await admission.CompleteAsync(response, true, false, cancellationToken);
                return new(WiseOldManCompetitionWriteStatus.Unauthorized, ErrorCode: "InvalidCredentials", Message: "Wise Old Man did not accept the competition management credentials.");
            }
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                await admission.CompleteAsync(response, true, false, cancellationToken);
                return new(WiseOldManCompetitionWriteStatus.RateLimited, RetryAt: limiter.GetStatus().NextPermittedAt, ErrorCode: "RateLimited", Message: "Wise Old Man is temporarily busy.");
            }
            if (!response.IsSuccessStatusCode)
            {
                await admission.CompleteAsync(response, true, true, cancellationToken);
                return new(WiseOldManCompetitionWriteStatus.Unknown, RetryAt: NextRetryAt(), ErrorCode: "UnknownOutcome", Message: "The Wise Old Man deletion outcome is unknown.");
            }
            await admission.CompleteAsync(response, true, false, cancellationToken);
            return new(WiseOldManCompetitionWriteStatus.Success, Competition: new WiseOldManCompetition(competitionId, string.Empty, DateTimeOffset.MinValue, DateTimeOffset.MaxValue, time.GetUtcNow(), []));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await admission.CompleteAsync(null, false, true, CancellationToken.None);
            return new(WiseOldManCompetitionWriteStatus.Unknown, RetryAt: NextRetryAt(), ErrorCode: "UnknownOutcome", Message: "The Wise Old Man deletion outcome is unknown.");
        }
        catch (HttpRequestException)
        {
            await admission.CompleteAsync(null, false, true, CancellationToken.None);
            return new(WiseOldManCompetitionWriteStatus.Unknown, RetryAt: NextRetryAt(), ErrorCode: "UnknownOutcome", Message: "The Wise Old Man deletion outcome is unknown.");
        }
    }

    private async Task<WiseOldManCompetitionWriteResult> ParseSuccessAsync(HttpResponseMessage response, WiseOldManCompetitionWritePayload requested, CancellationToken cancellationToken)
    {
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var root = document.RootElement;
        var competitionElement = root.TryGetProperty("competition", out var nested) ? nested : root;
        var id = competitionElement.TryGetProperty("id", out var idValue) && idValue.TryGetInt64(out var parsedId) ? parsedId : 0;
        if (id <= 0) return new(WiseOldManCompetitionWriteStatus.Unknown, ErrorCode: "MalformedSuccess", Message: "Wise Old Man returned no competition identity.");
        var title = competitionElement.TryGetProperty("title", out var titleValue) && titleValue.ValueKind == JsonValueKind.String ? titleValue.GetString() : requested.Title;
        var startsAt = ParseDate(competitionElement, "startsAt") ?? requested.StartsAt;
        var endsAt = ParseDate(competitionElement, "endsAt") ?? requested.EndsAt;
        var code = FirstString(root, "verificationCode", "verification_code", "code");
        var competition = new WiseOldManCompetition(id, title?.Trim() ?? requested.Title, startsAt, endsAt, time.GetUtcNow(), []);
            return new(WiseOldManCompetitionWriteStatus.Success, competition, ProtectedVerificationCode: string.IsNullOrWhiteSpace(code) ? null : credentialProtector.Protect(code), Message: "Wise Old Man competition write succeeded.");
    }

    private static async Task<(string? Code, string? Message, IReadOnlyList<string>? AffectedParticipants)> ParseErrorAsync(HttpResponseMessage response, string? verificationCode, CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = document.RootElement;
            var code = FirstString(root, "code", "error", "type");
            var message = FirstString(root, "message", "errorMessage", "detail");
            var affected = root.TryGetProperty("affectedPlayers", out var players) && players.ValueKind == JsonValueKind.Array
                ? players.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).Take(100).ToArray()
                : null;
            return (SanitizeProviderText(code, verificationCode), SanitizeProviderText(message, verificationCode), affected);
        }
        catch (JsonException) { return (null, null, null); }
    }

    private static string? SanitizeProviderText(string? value, string? verificationCode)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var sanitized = string.IsNullOrWhiteSpace(verificationCode)
            ? value.Trim()
            : value.Replace(verificationCode, "[redacted]", StringComparison.Ordinal);
        return sanitized.Length <= 500 ? sanitized : sanitized[..500];
    }

    private static object ToProviderPayload(WiseOldManCompetitionWritePayload payload, string? verificationCode)
        => payload.IncludeTeams
            ? new
            {
                title = payload.Title,
                metric = "ehb",
                startsAt = payload.StartsAt.ToUniversalTime().ToString("O"),
                endsAt = payload.EndsAt.ToUniversalTime().ToString("O"),
                teams = payload.Teams.Select(team => new { name = team.Name, participants = team.Participants }).ToArray(),
                verificationCode
            }
            : new
            {
                title = payload.Title,
                metric = "ehb",
                startsAt = payload.StartsAt.ToUniversalTime().ToString("O"),
                endsAt = payload.EndsAt.ToUniversalTime().ToString("O"),
                verificationCode
            };

    private static string? FirstString(JsonElement element, params string[] names)
        => names.Select(name => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static DateTimeOffset? ParseDate(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), out var parsed)
            ? parsed.ToUniversalTime()
            : null;

    private DateTimeOffset? NextRetryAt() => limiter.GetStatus().NextPermittedAt ?? limiter.GetStatus().ResetAt;
}
