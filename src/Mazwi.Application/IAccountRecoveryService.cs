namespace Mazwi.Application;

public interface IAccountRecoveryService
{
    // Always completes the same way, so the response never reveals whether an account exists.
    Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken);
    Task<bool> ResetPasswordAsync(string token, string password, CancellationToken cancellationToken);
    // False when the artist is missing or already verified.
    Task<bool> SendVerificationAsync(Guid artistId, CancellationToken cancellationToken);
    Task<bool> VerifyEmailAsync(string token, CancellationToken cancellationToken);
}
