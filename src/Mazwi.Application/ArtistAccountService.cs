using Mazwi.Domain;

namespace Mazwi.Application;

public sealed class ArtistAccountService(IArtistRepository artists, IStoryRepository stories, IPasswordHasher hasher) : IArtistAccountService
{
    public async Task<ArtistDto> SignUpAsync(string email, string password, CancellationToken cancellationToken)
    {
        var address = new EmailAddress(email);
        Artist artist = Artist.Register(address, new NewPassword(password), hasher);
        if (!await artists.AddAsync(artist, cancellationToken))
        {
            throw new AccountExistsException();
        }

        return ArtistDto.From(artist);
    }

    public async Task<ArtistDto?> SignInAsync(string email, string password, CancellationToken cancellationToken)
    {
        EmailAddress address;
        try
        {
            address = new EmailAddress(email);
        }
        catch (DomainException)
        {
            return null;
        }

        Artist? artist = await artists.FindByEmailAsync(address, cancellationToken);
        if (artist is null)
        {
            // Hash anyway so an unknown email takes as long as a wrong password and does not reveal which accounts exist.
            hasher.Hash(new NewPassword("unknown-account-timing-equaliser"));
            return null;
        }

        return artist.CanSignIn(password, hasher) ? ArtistDto.From(artist) : null;
    }

    public async Task<ArtistDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        Artist? artist = await artists.GetAsync(id, cancellationToken);
        return artist is null ? null : ArtistDto.From(artist);
    }

    public async Task<int> ClaimUnownedStoriesAsync(string email, CancellationToken cancellationToken)
    {
        Artist artist = await artists.FindByEmailAsync(new EmailAddress(email), cancellationToken)
            ?? throw new DomainException("No account uses that email address. Sign up first.");
        return await stories.ClaimUnownedAsync(artist.Id, cancellationToken);
    }
}
