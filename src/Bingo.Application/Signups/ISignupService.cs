namespace Bingo.Application.Signups;

public interface ISignupService
{
    Task<SignupResult> SignUpAsync(SignupRequest request, CancellationToken cancellationToken = default);

    Task<int> IncreaseCapacityAndPromoteAsync(Guid eventId, int newCap, CancellationToken cancellationToken = default);

    Task<int> PromoteAvailablePlacesAsync(Guid eventId, CancellationToken cancellationToken = default);
}
