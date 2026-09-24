namespace Bingo.Application.Events;

public interface IEventCompetitionUpdateAllService
{
    Task ProcessDueAsync(CancellationToken cancellationToken = default);
}
