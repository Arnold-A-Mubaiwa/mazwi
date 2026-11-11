namespace Mazwi.Domain;

public interface IArtistRepository
{
    // Returns false when the email address is already registered.
    Task<bool> AddAsync(Artist artist, CancellationToken cancellationToken);
    Task<Artist?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<Artist?> FindByEmailAsync(EmailAddress email, CancellationToken cancellationToken);
}
