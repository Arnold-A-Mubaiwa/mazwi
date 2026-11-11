using Mazwi.Domain;

namespace Mazwi.Application;

public sealed class AccountRecoveryService(IArtistRepository artists, IAccountTokenRepository tokens, IEmailSender email, IPasswordHasher hasher,
    TimeProvider clock, AccountLinks links) : IAccountRecoveryService
{
    public async Task RequestPasswordResetAsync(string emailAddress, CancellationToken cancellationToken)
    {
        EmailAddress address;
        try
        {
            address = new EmailAddress(emailAddress);
        }
        catch (DomainException)
        {
            return;
        }

        Artist? artist = await artists.FindByEmailAsync(address, cancellationToken);
        if (artist is null)
        {
            return;
        }

        (AccountToken token, string secret) = AccountToken.Issue(artist.Id, AccountTokenPurpose.PasswordReset, clock.GetUtcNow());
        await tokens.ReplaceAsync(token, cancellationToken);
        await email.SendAsync(new EmailMessage(artist.Email, "Reset your Mazwi password",
            $"""
            Someone asked to reset the password for your Mazwi account.

            Choose a new password here (the link works once, for one hour):
            {links.For("/reset-password", secret)}

            If this was not you, ignore this email. Your password has not changed.
            """), cancellationToken);
    }

    public async Task<bool> ResetPasswordAsync(string token, string password, CancellationToken cancellationToken)
    {
        var newPassword = new NewPassword(password);
        (AccountToken? found, Artist? artist) = await FindUsableAsync(token, AccountTokenPurpose.PasswordReset, cancellationToken);
        if (found is null || artist is null)
        {
            return false;
        }

        found.Use(clock.GetUtcNow());
        artist.ChangePassword(newPassword, hasher);
        // Opening the reset link proves the artist reads this inbox.
        artist.VerifyEmail(clock.GetUtcNow());
        await artists.SaveSecurityAsync(artist, found, cancellationToken);
        return true;
    }

    public async Task<bool> SendVerificationAsync(Guid artistId, CancellationToken cancellationToken)
    {
        Artist? artist = await artists.GetAsync(artistId, cancellationToken);
        if (artist is null || artist.EmailVerified)
        {
            return false;
        }

        (AccountToken token, string secret) = AccountToken.Issue(artist.Id, AccountTokenPurpose.EmailVerification, clock.GetUtcNow());
        await tokens.ReplaceAsync(token, cancellationToken);
        await email.SendAsync(new EmailMessage(artist.Email, "Confirm your email for Mazwi",
            $"""
            Welcome to Mazwi.

            Confirm this is your email address (the link works once, for three days):
            {links.For("/verify-email", secret)}

            If you did not create a Mazwi account, ignore this email.
            """), cancellationToken);
        return true;
    }

    public async Task<bool> VerifyEmailAsync(string token, CancellationToken cancellationToken)
    {
        (AccountToken? found, Artist? artist) = await FindUsableAsync(token, AccountTokenPurpose.EmailVerification, cancellationToken);
        if (found is null || artist is null)
        {
            return false;
        }

        found.Use(clock.GetUtcNow());
        artist.VerifyEmail(clock.GetUtcNow());
        await artists.SaveSecurityAsync(artist, found, cancellationToken);
        return true;
    }

    private async Task<(AccountToken? Token, Artist? Artist)> FindUsableAsync(string secret, AccountTokenPurpose purpose, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(secret) || secret.Length > 200)
        {
            return (null, null);
        }

        AccountToken? token = await tokens.FindAsync(AccountToken.HashOf(secret), purpose, cancellationToken);
        if (token is null || !token.IsUsable(clock.GetUtcNow()))
        {
            return (null, null);
        }

        return (token, await artists.GetAsync(token.ArtistId, cancellationToken));
    }
}
