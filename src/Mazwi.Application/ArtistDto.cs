using Mazwi.Domain;

namespace Mazwi.Application;

public sealed record ArtistDto(Guid Id, string Email)
{
    public static ArtistDto From(Artist artist) => new(artist.Id, artist.Email.Value);
}
