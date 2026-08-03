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
        "Rasmus Zebak", "Rasmus Activity Main", "crunch704", "ZemaFios", "Detoned", "Frette", "Thuebob",
        "Spacecreator", "Raffineret", "I use x22", "Kongherodes", "itsMKN", "Gimgonduth", "Corgisiron",
        "NoobNicoline", "Zop1", "zakk0", "Mikkel-IT", "Thylegend", "Ezzi", "W olles", "Completeius",
        "Calm Chris", "IM Latry", "im iftic", "Zanshock", "stoltze", "Myrupz", "Maxzen", "Bubber",
        "Freakingpand", "Karl Knast", "Macdroppet", "N L C K O", "Siswet19", "Sunny Boy110", "oegget",
        "3lite men x", "200iq p2W", "Ricebarrage", "Mrtopfresh", "MindMySnipe", "GIM Wemox", "Røllemester",
        "Maxe2968", "Backshotbaby", "Agent Groth", "Agent Slidt", "R33Con", "pappresseren", "Elite ca",
        "Jern Jakob", "MesterMudder", "Sanddrage", "MrDryhard", "User IM", "Skade", "Tanzania Tim",
        "Uganda ulrik", "Kenya Kaj", "BotF"
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
        if (competitionId != 1515) return Response(HttpStatusCode.NotFound, "{}");
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
            .Where(name => !string.Equals(mode, "Incomplete", StringComparison.OrdinalIgnoreCase) || !string.Equals(name, "Rasmus Activity Main", StringComparison.OrdinalIgnoreCase))
            .Select((name, index) => new
            {
                player = new { username = name, type = "REGULAR" },
                deltas = new[] { new { metric = "ehb", values = new { gained = name == "Rasmus Zebak" ? 12.0m : name == "Rasmus Activity Main" ? 8.0m : 2.0m + index % 3 } } }
            });
        var now = time.GetUtcNow();
        return Response(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            id = competitionId,
            title = "TEST 15 local fake competition",
            startsAt = now.AddHours(-1),
            endsAt = now.AddDays(5),
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
