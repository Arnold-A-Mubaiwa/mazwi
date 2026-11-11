using System.Security.Claims;

namespace Mazwi.WebApi;

public static class ArtistClaims
{
    public const string SecurityStamp = "mazwi:stamp";

    public static Guid ArtistId(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out Guid id) ? id : Guid.Empty;
    }

    public static Guid Stamp(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Guid.TryParse(user.FindFirstValue(SecurityStamp), out Guid stamp) ? stamp : Guid.Empty;
    }

    public static ClaimsPrincipal For(Guid artistId, string email, string scheme, Guid securityStamp = default) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, artistId.ToString()), new Claim(ClaimTypes.Email, email),
            new Claim(SecurityStamp, securityStamp.ToString())], scheme));
}
