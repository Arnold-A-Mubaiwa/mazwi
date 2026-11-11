namespace Mazwi.Application;

public interface IArtistAccountService
{
    Task<ArtistDto> SignUpAsync(string email, string password, CancellationToken cancellationToken);
    // Returns null for an unknown email or a wrong password, without saying which.
    Task<ArtistDto?> SignInAsync(string email, string password, CancellationToken cancellationToken);
    Task<ArtistDto?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<int> ClaimUnownedStoriesAsync(string email, CancellationToken cancellationToken);
}
