using Bingo.Application.Events;
using Bingo.Infrastructure.WiseOldMan;
using Bingo.Web.Operations;
using Microsoft.Extensions.Options;

namespace Bingo.Web.Events;

public sealed partial class EventCompetitionSynchronizationWorker(
    IServiceScopeFactory scopes,
    TimeProvider time,
    IHostEnvironment environment,
    IOptions<WiseOldManOptions> wiseOldManOptions,
    WorkerHeartbeatRegistry heartbeats,
    ILogger<EventCompetitionSynchronizationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            heartbeats.Beat(WorkerHeartbeatRegistry.CompetitionSynchronizationWorker);
            try
            {
                if (environment.IsDevelopment() && !wiseOldManOptions.Value.DevelopmentFake.AutomaticSynchronizationEnabled)
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), time, stoppingToken);
                    continue;
                }
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IEventCompetitionSynchronizationService>().ProcessDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { LogFailure(logger, exception); }
            await Task.Delay(TimeSpan.FromSeconds(30), time, stoppingToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Scheduled Wise Old Man competition synchronization failed.")]
    private static partial void LogFailure(ILogger logger, Exception exception);
}
