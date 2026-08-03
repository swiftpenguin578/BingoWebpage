using System.Net.Http.Headers;
using Bingo.Application.Integrations.WiseOldMan;
using Microsoft.Extensions.Logging;

namespace Bingo.Infrastructure.WiseOldMan;

public sealed class WiseOldManRequestLimiter(TimeProvider time, ILogger<WiseOldManRequestLimiter> logger)
    : IDisposable
{
    private const int Reserve = 3;
    private static readonly TimeSpan BootstrapFailurePause = TimeSpan.FromMinutes(1);
    private readonly SemaphoreSlim gate = new(1, 1);
    private int? observedLimit;
    private int? observedRemaining;
    private DateTimeOffset? resetAt;
    private DateTimeOffset? nextPermittedAt;
    private DateTimeOffset? lastRequestAt;
    private DateTimeOffset? lastSuccessAt;
    private DateTimeOffset? lastErrorAt;
    private DateTimeOffset? lastRateLimitedAt;
    private bool hasObservedWindow;
    private static readonly Action<ILogger, DateTimeOffset?, bool, bool, bool, Exception?> LogFailedClosed =
        LoggerMessage.Define<DateTimeOffset?, bool, bool, bool>(LogLevel.Warning, new EventId(10101), "Wise Old Man limiter failed closed until {NextPermittedAt}; transport={TransportFailure}, headers={HeadersValid}, payload={PayloadValid}");

    public WiseOldManRequestStatus GetStatus() => new(
        observedLimit, observedRemaining, resetAt, lastRequestAt, lastSuccessAt,
        lastErrorAt, lastRateLimitedAt, nextPermittedAt);

    public async Task<WiseOldManAdmission> AdmitAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        var now = time.GetUtcNow();
        lastRequestAt = now;
        if (nextPermittedAt is { } blockedUntil && blockedUntil > now)
            return WiseOldManAdmission.Rejected(this, blockedUntil);

        if (hasObservedWindow && resetAt is { } knownReset && knownReset <= now)
        {
            hasObservedWindow = false;
            observedRemaining = null;
            resetAt = null;
        }

        if (hasObservedWindow && (observedRemaining is null || observedRemaining <= Reserve))
            return WiseOldManAdmission.Rejected(this, nextPermittedAt ?? resetAt ?? now.Add(BootstrapFailurePause));

        // The gate remains held until CompleteAsync, so only one bootstrap request can be in flight.
        return WiseOldManAdmission.Allowed(this);
    }

    public async Task CompleteAsync(
        HttpResponseMessage? response,
        bool validPayload,
        bool transportFailure,
        CancellationToken cancellationToken)
    {
        try
        {
            var now = time.GetUtcNow();
            var headersValid = TryReadHeaders(response, now, out var limit, out var remaining, out var reset);
            var retryAfter = TryReadSeconds(response?.Headers, "Retry-After", now);
            var isRateLimited = response?.StatusCode == System.Net.HttpStatusCode.TooManyRequests;
            if (headersValid)
            {
                observedLimit = limit;
                observedRemaining = remaining;
                resetAt = reset;
                hasObservedWindow = true;
            }

            if (isRateLimited)
            {
                lastRateLimitedAt = now;
                lastErrorAt = now;
                if (!headersValid)
                {
                    hasObservedWindow = false;
                    observedRemaining = null;
                    resetAt = null;
                }
                nextPermittedAt = retryAfter ?? (headersValid ? reset : now.Add(BootstrapFailurePause));
            }
            else if (transportFailure || !headersValid || !validPayload)
            {
                hasObservedWindow = false;
                observedRemaining = null;
                resetAt = null;
                nextPermittedAt = now.Add(BootstrapFailurePause);
                lastErrorAt = now;
                LogFailedClosed(logger, nextPermittedAt, transportFailure, headersValid, validPayload, null);
            }
            else
            {
                nextPermittedAt = null;
                if (response?.IsSuccessStatusCode == true) lastSuccessAt = now;
                else lastErrorAt = now;
            }
        }
        finally
        {
            gate.Release();
            await Task.CompletedTask;
        }
    }

    private static bool TryReadHeaders(HttpResponseMessage? response, DateTimeOffset observedAt, out int limit, out int remaining, out DateTimeOffset resetAt)
    {
        limit = 0; remaining = 0; resetAt = default;
        if (response is null || !TryReadInt(response.Headers, "RateLimit-Limit", out limit) || !TryReadInt(response.Headers, "RateLimit-Remaining", out remaining) || !TryReadSeconds(response.Headers, "RateLimit-Reset", observedAt, out var reset)) return false;
        resetAt = reset;
        return limit >= 0 && remaining >= 0;
    }

    private static bool TryReadInt(HttpResponseHeaders headers, string name, out int value)
    {
        value = 0;
        return headers.TryGetValues(name, out var values) && int.TryParse(values.FirstOrDefault(), out value);
    }

    private static DateTimeOffset? TryReadSeconds(HttpResponseHeaders? headers, string name, DateTimeOffset observedAt) =>
        TryReadSeconds(headers, name, observedAt, out var value) ? value : null;

    private static bool TryReadSeconds(HttpResponseHeaders? headers, string name, DateTimeOffset observedAt, out DateTimeOffset value)
    {
        value = default;
        if (headers is null || !headers.TryGetValues(name, out var values) || !double.TryParse(values.FirstOrDefault(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds) || seconds < 0 || seconds > 86_400) return false;
        value = observedAt.AddSeconds(seconds);
        return true;
    }

    public void Dispose() => gate.Dispose();

    public sealed class WiseOldManAdmission : IAsyncDisposable
    {
        private readonly WiseOldManRequestLimiter owner;
        private int completed;
        private WiseOldManAdmission(WiseOldManRequestLimiter owner, bool allowed, DateTimeOffset? retryAt, bool completed)
        {
            this.owner = owner;
            AllowedRequest = allowed;
            RetryAt = retryAt;
            this.completed = completed ? 1 : 0;
        }
        public bool AllowedRequest { get; }
        public DateTimeOffset? RetryAt { get; }
        public static WiseOldManAdmission Allowed(WiseOldManRequestLimiter owner) => new(owner, true, null, false);
        public static WiseOldManAdmission Rejected(WiseOldManRequestLimiter owner, DateTimeOffset retryAt)
        {
            owner.gate.Release();
            return new(owner, false, retryAt, true);
        }

        public Task CompleteAsync(HttpResponseMessage? response, bool validPayload, bool transportFailure, CancellationToken ct)
        {
            if (Interlocked.Exchange(ref completed, 1) != 0) return Task.CompletedTask;
            return owner.CompleteAsync(response, validPayload, transportFailure, ct);
        }

        public async ValueTask DisposeAsync()
        {
            if (Volatile.Read(ref completed) == 0)
                await CompleteAsync(null, false, true, CancellationToken.None);
        }
    }
}
