using Mazwi.Application;
using Mazwi.WebApi.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Mazwi.WebApi.Controllers;

[ApiController]
[Route("api/account")]
[RequestSizeLimit(10_000)]
public sealed class AccountController(IArtistAccountService service) : ControllerBase
{
    [HttpPost("signup")]
    [EnableRateLimiting(AccountLimits.Policy)]
    public async Task<ActionResult<ArtistResponse>> SignUp(AccountRequest request, CancellationToken cancellationToken)
    {
        ArtistDto artist = await service.SignUpAsync(request.Email, request.Password, cancellationToken);
        await SignInCookieAsync(artist);
        return Ok(ArtistResponse.From(artist));
    }

    [HttpPost("signin")]
    [EnableRateLimiting(AccountLimits.Policy)]
    public async Task<ActionResult<ArtistResponse>> SignIn(AccountRequest request, CancellationToken cancellationToken)
    {
        ArtistDto? artist = await service.SignInAsync(request.Email, request.Password, cancellationToken);
        if (artist is null)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sign-in failed.", detail: "That email and password do not match an account.");
        }

        await SignInCookieAsync(artist);
        return Ok(ArtistResponse.From(artist));
    }

    [HttpPost("signout")]
    public async Task<IActionResult> SignOutArtist()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ArtistResponse>> Me(CancellationToken cancellationToken)
    {
        ArtistDto? artist = await service.GetAsync(User.ArtistId(), cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return artist is null ? Unauthorized() : Ok(ArtistResponse.From(artist));
    }

    private Task SignInCookieAsync(ArtistDto artist) => HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
        ArtistClaims.For(artist.Id, artist.Email, CookieAuthenticationDefaults.AuthenticationScheme));
}
