using Bingo.Domain.Access;
using Microsoft.AspNetCore.Authorization;

namespace Bingo.Web.Security;

public sealed class AccountAccessRequirement(AccountAccessMode minimumMode) : IAuthorizationRequirement
{
    public AccountAccessMode MinimumMode { get; } = minimumMode;
}
