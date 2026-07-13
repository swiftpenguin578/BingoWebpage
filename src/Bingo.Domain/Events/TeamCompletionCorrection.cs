namespace Bingo.Domain.Events;

public sealed class TeamCompletionCorrection
{
    private TeamCompletionCorrection() { }
    public TeamCompletionCorrection(Guid id,Guid eventId,Guid teamId,DateTimeOffset correctedAt,string reason,Guid correctedByAccountId,DateTimeOffset recordedAt)
    { if(string.IsNullOrWhiteSpace(reason))throw new ArgumentException("A reason is required.",nameof(reason));Id=id;EventId=eventId;TeamId=teamId;CorrectedCompletedAt=correctedAt.ToUniversalTime();Reason=reason.Trim();CorrectedByAccountId=correctedByAccountId;RecordedAt=recordedAt.ToUniversalTime(); }
    public Guid Id{get;private set;} public Guid EventId{get;private set;} public Guid TeamId{get;private set;} public DateTimeOffset CorrectedCompletedAt{get;private set;} public string Reason{get;private set;}=string.Empty; public Guid CorrectedByAccountId{get;private set;} public DateTimeOffset RecordedAt{get;private set;}
    public void Update(DateTimeOffset correctedAt,string reason,Guid accountId,DateTimeOffset recordedAt){if(string.IsNullOrWhiteSpace(reason))throw new ArgumentException("A reason is required.",nameof(reason));CorrectedCompletedAt=correctedAt.ToUniversalTime();Reason=reason.Trim();CorrectedByAccountId=accountId;RecordedAt=recordedAt.ToUniversalTime();}
}
