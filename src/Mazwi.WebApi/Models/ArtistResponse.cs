using Mazwi.Application;

namespace Mazwi.WebApi.Models;

public sealed record ArtistResponse(Guid Id, string Email)
{
    public static ArtistResponse From(ArtistDto artist) => new(artist.Id, artist.Email);
}
