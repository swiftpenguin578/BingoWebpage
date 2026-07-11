namespace Bingo.Domain.Signups;

public sealed class SignupQuestion
{
    private SignupQuestion() { }
    public SignupQuestion(Guid id, Guid eventId, string key, string label, SignupQuestionType type, bool required, int position, string? options)
    {
        Id = id; EventId = eventId; Key = key; Label = label; Type = type; Required = required; Position = position; Options = options; Active = true;
    }
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public string? HelpText { get; private set; }
    public SignupQuestionType Type { get; private set; }
    public bool Required { get; private set; }
    public int Position { get; private set; }
    public string? Options { get; private set; }
    public bool Active { get; private set; }

    public void Deactivate() => Active = false;
}
