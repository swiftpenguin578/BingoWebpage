using Bingo.Domain.Auditing;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Events;

public sealed partial class EventLifecycleWorker(IServiceScopeFactory scopes,TimeProvider time,ILogger<EventLifecycleWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            try{await EndScheduledEvents(stoppingToken);}catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){break;}catch(Exception exception){LogFailure(logger,exception);}
            await Task.Delay(TimeSpan.FromSeconds(30),time,stoppingToken);
        }
    }
    private async Task EndScheduledEvents(CancellationToken ct)
    {
        await using var scope=scopes.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();var now=time.GetUtcNow();var events=await db.Events.Where(x=>x.State==EventState.Live&&x.EventEndsAt<=now).ToListAsync(ct);foreach(var ev in events){ev.EndEvent();db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(),now,null,"System","event.ended_automatically","event",ev.Id.ToString(),$"Scheduled end reached at {ev.EventEndsAt:O}"));}if(events.Count>0)await db.SaveChangesAsync(ct);
    }
    [LoggerMessage(Level=LogLevel.Error,Message="Scheduled event lifecycle update failed.")]
    private static partial void LogFailure(ILogger logger,Exception exception);
}
