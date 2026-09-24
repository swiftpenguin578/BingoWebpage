using Bingo.Application.Events;
using Microsoft.AspNetCore.DataProtection;

namespace Bingo.Web.Security;

public sealed class DataProtectionCompetitionCredentialProtector(IDataProtectionProvider provider) : ICompetitionCredentialProtector
{
    private readonly IDataProtector protector = provider.CreateProtector("Bingo.WiseOldMan.ManagedCompetitionCode.v1");

    public string Protect(string verificationCode) => protector.Protect(verificationCode);

    public string Unprotect(string protectedCode) => protector.Unprotect(protectedCode);
}
