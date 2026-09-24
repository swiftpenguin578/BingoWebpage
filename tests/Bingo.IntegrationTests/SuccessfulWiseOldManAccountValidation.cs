using Bingo.Application.Integrations.WiseOldMan;

namespace Bingo.IntegrationTests;

internal sealed class SuccessfulWiseOldManAccountValidation : IWiseOldManAccountValidation
{
    public Task<WiseOldManAccountValidationResult> ValidateAsync(
        WiseOldManAccountValidationRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new WiseOldManAccountValidationResult(WiseOldManAccountValidationOutcome.Success, []));
}

internal sealed class SuccessfulWiseOldManPlayerLookup : IWiseOldManPlayerLookup
{
    public Task<WiseOldManPlayerLookupResult> LookupPlayerAsync(string characterName, CancellationToken cancellationToken = default) =>
        Task.FromResult(new WiseOldManPlayerLookupResult(WiseOldManLookupStatus.Success, 12m, DateTimeOffset.UtcNow));
}
