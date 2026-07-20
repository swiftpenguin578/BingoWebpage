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
            try{await ApplyScheduledTransitions(stoppingToken);}catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){break;}catch(Exception exception){LogFailure(logger,exception);}
            await Task.Delay(TimeSpan.FromSeconds(30),time,stoppingToken);
        }
    }
    private async Task ApplyScheduledTransitions(CancellationToken ct)
    {
        await using var scope=scopes.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now=time.GetUtcNow();
        var events=await db.Events
            .Where(x=>(x.State==EventState.Draft&&x.SignupOpensAt<=now&&x.SignupClosesAt>now)
                ||(x.State==EventState.SignupOpen&&x.SignupClosesAt<=now)
                ||(x.State==EventState.Live&&x.EventEndsAt<=now))
            .ToListAsync(ct);

        foreach(var ev in events)
        {
            if(ev.OpenSignupsIfScheduled(now))
            {
                db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(),now,null,"System","event.signups_opened_automatically","event",ev.Id.ToString(),$"Scheduled opening reached at {ev.SignupOpensAt:O}"));
            }
            else if(ev.CloseSignupsIfScheduled(now))
            {
                db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(),now,null,"System","event.signups_closed_automatically","event",ev.Id.ToString(),$"Scheduled closing reached at {ev.SignupClosesAt:O}"));
            }
            else if(ev.State==EventState.Live&&ev.EventEndsAt<=now)
            {
                ev.EndEvent();
                db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(),now,null,"System","event.ended_automatically","event",ev.Id.ToString(),$"Scheduled end reached at {ev.EventEndsAt:O}"));
            }
        }

        if(events.Count>0)await db.SaveChangesAsync(ct);
    }
    [LoggerMessage(Level=LogLevel.Error,Message="Scheduled event lifecycle update failed.")]
    private static partial void LogFailure(ILogger logger,Exception exception);
}
