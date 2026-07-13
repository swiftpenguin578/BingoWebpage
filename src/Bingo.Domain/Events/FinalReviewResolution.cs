namespace Bingo.Domain.Events;

public sealed class FinalReviewResolution
{
    private FinalReviewResolution() { }
    public FinalReviewResolution(Guid id,Guid eventId,string blockerKey,string blockerDescription,string reason,Guid resolvedByAccountId,DateTimeOffset resolvedAt)
    { if(string.IsNullOrWhiteSpace(blockerKey)||string.IsNullOrWhiteSpace(reason))throw new ArgumentException("A blocker and reason are required.");Id=id;EventId=eventId;BlockerKey=blockerKey;BlockerDescription=blockerDescription;Reason=reason.Trim();ResolvedByAccountId=resolvedByAccountId;ResolvedAt=resolvedAt.ToUniversalTime(); }
    public Guid Id{get;private set;} public Guid EventId{get;private set;} public string BlockerKey{get;private set;}=string.Empty; public string BlockerDescription{get;private set;}=string.Empty; public string Reason{get;private set;}=string.Empty; public Guid ResolvedByAccountId{get;private set;} public DateTimeOffset ResolvedAt{get;private set;}
}
