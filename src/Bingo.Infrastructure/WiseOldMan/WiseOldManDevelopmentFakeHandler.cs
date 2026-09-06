using System.Net;
using System.Text;
using System.Text.Json;

namespace Bingo.Infrastructure.WiseOldMan;

public sealed class WiseOldManDevelopmentFakeHandler(
    WiseOldManOptions options,
    TimeProvider time) : HttpMessageHandler
{
    private static readonly string[] CompetitionPlayers =
    [
        "Dev Player 001", "Dev Activity Secondary", "Dev Player 002", "Dev Player 003", "Dev Player 004", "Dev Player 005", "Dev Player 006", "Dev Player 007", "Dev Player 008", "Dev Player 009", "Dev Player 010",
        "Dev Player 011", "Dev Player 012", "Dev Player 013", "Dev Player 014", "Dev Player 015", "Dev Player 016", "Dev Player 017", "Dev Player 018", "Dev Player 019", "Dev Player 020",
        "Dev Player 021", "Dev Player 022", "Dev Player 023", "Dev Player 024", "Dev Player 025", "Dev Player 026", "Dev Player 027", "Dev Player 028", "Dev Player 029", "Dev Player 030",
        "Dev Player 031", "Dev Player 032", "Dev Player 033", "Dev Player 034", "Dev Player 035", "Dev Player 036", "Dev Player 037", "Dev Player 038", "Dev Player 039", "Dev Player 040",
        "Dev Player 041", "Dev Player 042", "Dev Player 043", "Dev Player 044", "Dev Player 045", "Dev Player 046", "Dev Player 047", "Dev Player 048", "Dev Player 049", "Dev Player 050",
        "Dev Player 051", "Dev Player 052", "Dev Player 053", "Dev Player 054", "Dev Player 055", "Dev Player 056", "Dev Player 057", "Dev Player 058", "Dev Player 059", "Dev Player 060"
    ];
    private int temporaryFailuresRemaining = Math.Max(0, options.DevelopmentFake.TemporaryFailures);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        var routeStart = path.Length > 0 && string.Equals(path[0], "v2", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        if (path.Length > routeStart + 1 && string.Equals(path[routeStart], "players", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(PlayerResponse(Uri.UnescapeDataString(path[routeStart + 1])));
        if (path.Length > routeStart + 1 && string.Equals(path[routeStart], "competitions", StringComparison.OrdinalIgnoreCase) && long.TryParse(path[routeStart + 1], out var competitionId))
            return Task.FromResult(CompetitionResponse(competitionId));
        return Task.FromResult(Response(HttpStatusCode.NotFound, "{}"));
    }

    private HttpResponseMessage PlayerResponse(string username)
    {
        var mode = options.DevelopmentFake.PlayerMode.Trim();
        if (string.Equals(mode, "NotFound", StringComparison.OrdinalIgnoreCase) || username.Contains("missing", StringComparison.OrdinalIgnoreCase))
            return Response(HttpStatusCode.NotFound, "{}");
        if (string.Equals(mode, "Unavailable", StringComparison.OrdinalIgnoreCase))
            return Response(HttpStatusCode.ServiceUnavailable, "{}");
        if (string.Equals(mode, "RateLimited", StringComparison.OrdinalIgnoreCase))
            return RateLimitedResponse();
        var ehb = username.Contains("main", StringComparison.OrdinalIgnoreCase) ? 8.0m : 12.5m;
        return Response(HttpStatusCode.OK, JsonSerializer.Serialize(new { ehb }));
    }

    private HttpResponseMessage CompetitionResponse(long competitionId)
    {
        if (competitionId is not (1515 or 1516)) return Response(HttpStatusCode.NotFound, "{}");
        if (temporaryFailuresRemaining > 0)
        {
            temporaryFailuresRemaining--;
            return Response(HttpStatusCode.ServiceUnavailable, "{}");
        }
        var mode = options.DevelopmentFake.CompetitionMode.Trim();
        if (string.Equals(mode, "Unavailable", StringComparison.OrdinalIgnoreCase))
            return Response(HttpStatusCode.ServiceUnavailable, "{}");
        if (string.Equals(mode, "RateLimited", StringComparison.OrdinalIgnoreCase))
            return RateLimitedResponse();
        var players = CompetitionPlayers
            .Where(name => !string.Equals(mode, "Incomplete", StringComparison.OrdinalIgnoreCase) || !string.Equals(name, "Dev Activity Secondary", StringComparison.OrdinalIgnoreCase))
            .Select((name, index) => new
            {
                player = new { username = name, type = "REGULAR" },
                deltas = new[] { new { metric = "ehb", values = new { gained = name == "Dev Player 001" ? 12.0m : name == "Dev Activity Secondary" ? 8.0m : name == "Dev Player 002" ? 20.0m : 2.0m + index % 3 } } }
            });
        var now = time.GetUtcNow();
        var startsAt = now.AddHours(-99);
        var endsAt = competitionId == 1515 ? now.AddDays(14) : now.AddDays(15);
        return Response(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            id = competitionId,
            title = competitionId == 1515 ? "TEST 15 local fake competition" : "TEST 15 alternate local fake competition",
            startsAt,
            endsAt,
            updatedAt = now,
            participations = players
        }));
    }

    private HttpResponseMessage RateLimitedResponse()
    {
        var response = Response(HttpStatusCode.TooManyRequests, "{}");
        response.Headers.Add("Retry-After", Math.Max(1, options.DevelopmentFake.RetryAfterSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture));
        return response;
    }

    private HttpResponseMessage Response(HttpStatusCode status, string body)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        response.Headers.Add("RateLimit-Limit", "20");
        response.Headers.Add("RateLimit-Remaining", Math.Max(0, options.DevelopmentFake.Remaining).ToString(System.Globalization.CultureInfo.InvariantCulture));
        response.Headers.Add("RateLimit-Reset", "60");
        return response;
    }
}
