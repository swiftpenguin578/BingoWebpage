namespace Bingo.Domain.Signups;

public static class SignupQuestionHeadings
{
    public static IReadOnlyDictionary<Guid, string> Create(IEnumerable<SignupQuestion> questions)
    {
        var result = new Dictionary<Guid, string>();
        Add(EventCharacterRole.Playing, "Account");
        Add(EventCharacterRole.Informational, "Alt account");
        return result;

        void Add(EventCharacterRole role, string singular)
        {
            var accounts = questions.Where(question => question.Active && question.Type == SignupQuestionType.Account && question.AccountAnswerRole == role).OrderBy(question => question.Position).ToList();
            for (var index = 0; index < accounts.Count; index++) result[accounts[index].Id] = accounts.Count == 1 ? singular : $"{singular} {index + 1}";
        }
    }
}
