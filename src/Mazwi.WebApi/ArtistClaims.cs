using System.Security.Claims;

namespace Mazwi.WebApi;

public static class ArtistClaims
{
    public static Guid ArtistId(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out Guid id) ? id : Guid.Empty;
    }

    public static ClaimsPrincipal For(Guid artistId, string email, string scheme) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, artistId.ToString()), new Claim(ClaimTypes.Email, email)], scheme));
}
