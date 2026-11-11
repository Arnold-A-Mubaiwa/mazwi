using System.ComponentModel.DataAnnotations;

namespace Mazwi.WebApi.Models;

public sealed record PasswordResetRequest([Required, MaxLength(254)] string Email);

public sealed record PasswordResetConfirmation([Required, MaxLength(200)] string Token, [Required, MaxLength(128)] string Password);

public sealed record EmailVerificationRequest([Required, MaxLength(200)] string Token);
