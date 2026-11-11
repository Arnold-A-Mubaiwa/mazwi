using Mazwi.Domain;

namespace Mazwi.Application;

public sealed record ArtistDto(Guid Id, string Email, bool EmailVerified = false, Guid SecurityStamp = default)
{
    public static ArtistDto From(Artist artist) => new(artist.Id, artist.Email.Value, artist.EmailVerified, artist.SecurityStamp);
}
