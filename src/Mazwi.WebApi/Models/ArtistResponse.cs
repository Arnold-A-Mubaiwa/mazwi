using Mazwi.Application;

namespace Mazwi.WebApi.Models;

public sealed record ArtistResponse(Guid Id, string Email, bool EmailVerified = false, bool IsModerator = false)
{
    public static ArtistResponse From(ArtistDto artist, bool isModerator = false) => new(artist.Id, artist.Email, artist.EmailVerified, isModerator);
}
