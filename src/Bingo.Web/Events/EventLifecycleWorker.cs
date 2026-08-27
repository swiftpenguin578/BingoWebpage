using Bingo.Application.Events;
using Bingo.Web.Operations;

namespace Bingo.Web.Events;

public sealed partial class EventLifecycleWorker(IServiceScopeFactory scopes, TimeProvider time, WorkerHeartbeatRegistry heartbeats, ILogger<EventLifecycleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            heartbeats.Beat(WorkerHeartbeatRegistry.EventLifecycleWorker);
            try { await ApplyScheduledTransitions(stoppingToken); } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; } catch (Exception exception) { LogFailure(logger, exception); }
            await Task.Delay(TimeSpan.FromSeconds(30), time, stoppingToken);
        }
    }
    private async Task ApplyScheduledTransitions(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IEventLifecycleService>().ProcessDueAsync(ct);
        await scope.ServiceProvider.GetRequiredService<IEventBannerCleanupService>().ProcessPendingAsync(ct);
        await scope.ServiceProvider.GetRequiredService<EmergencyCredentialLifecycleService>().ApplyAsync(ct);
    }
    [LoggerMessage(Level = LogLevel.Error, Message = "Scheduled event lifecycle update failed.")]
    private static partial void LogFailure(ILogger logger, Exception exception);
}
