using Mazwi.Application;
using Microsoft.AspNetCore.Authorization;

namespace Mazwi.WebApi;

// Moderators are the accounts whose email is listed in Mazwi:Moderators AND confirmed, so nobody becomes a moderator by
// signing up with a listed address they cannot read.
public sealed class ModeratorRequirement : IAuthorizationRequirement
{
    public const string Policy = "Moderator";
}

public sealed class ModeratorHandler(IArtistAccountService accounts, IConfiguration configuration) : AuthorizationHandler<ModeratorRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ModeratorRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        Guid artistId = context.User.ArtistId();
        if (artistId == Guid.Empty)
        {
            return;
        }

        ArtistDto? artist = await accounts.GetAsync(artistId, CancellationToken.None);
        if (artist is { EmailVerified: true } && IsListed(configuration, artist.Email))
        {
            context.Succeed(requirement);
        }
    }

    public static bool IsListed(IConfiguration configuration, string email)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return (configuration["Mazwi:Moderators"] ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(listed => string.Equals(listed, email, StringComparison.OrdinalIgnoreCase));
    }
}
