using Mazwi.Application;
using Mazwi.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Mazwi.WebApi.Controllers;

[ApiController]
[Route("api/account")]
[RequestSizeLimit(10_000)]
[EnableRateLimiting(AccountLimits.Policy)]
public sealed class AccountRecoveryController(IAccountRecoveryService service) : ControllerBase
{
    // Always 202, whether or not the email belongs to an account.
    [HttpPost("password-reset")]
    public async Task<IActionResult> RequestReset(PasswordResetRequest request, CancellationToken cancellationToken)
    {
        await service.RequestPasswordResetAsync(request.Email, cancellationToken);
        return Accepted();
    }

    [HttpPost("password-reset/confirm")]
    public async Task<IActionResult> ConfirmReset(PasswordResetConfirmation request, CancellationToken cancellationToken) =>
        await service.ResetPasswordAsync(request.Token, request.Password, cancellationToken) ? NoContent() : InvalidLink();

    [HttpPost("verify-email")]
    public async Task<IActionResult> Verify(EmailVerificationRequest request, CancellationToken cancellationToken) =>
        await service.VerifyEmailAsync(request.Token, cancellationToken) ? NoContent() : InvalidLink();

    [HttpPost("verification-email")]
    [Authorize]
    public async Task<IActionResult> Resend(CancellationToken cancellationToken)
    {
        await service.SendVerificationAsync(User.ArtistId(), cancellationToken);
        return Accepted();
    }

    private ObjectResult InvalidLink() => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Link not valid.",
        detail: "This link has expired or was already used. Request a new one.");
}
