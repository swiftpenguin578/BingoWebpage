using Bingo.Application.Events;
using Bingo.Web.Operations;

namespace Bingo.Web.Events;

public sealed partial class EventCompetitionManagementWorker(
    IServiceScopeFactory scopes,
    TimeProvider time,
    WorkerHeartbeatRegistry heartbeats,
    ILogger<EventCompetitionManagementWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            heartbeats.Beat(WorkerHeartbeatRegistry.CompetitionManagementWorker);
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IEventCompetitionManagementService>().ProcessDueAsync(stoppingToken);
                await scope.ServiceProvider.GetRequiredService<IEventCompetitionUpdateAllService>().ProcessDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { LogFailure(logger, exception); }
            await Task.Delay(TimeSpan.FromSeconds(20), time, stoppingToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Scheduled Wise Old Man competition management failed.")]
    private static partial void LogFailure(ILogger logger, Exception exception);
}
