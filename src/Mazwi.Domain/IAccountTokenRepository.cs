namespace Mazwi.Domain;

public interface IAccountTokenRepository
{
    // Adds the token and retires every earlier unused token with the same artist and purpose.
    Task ReplaceAsync(AccountToken token, CancellationToken cancellationToken);
    Task<AccountToken?> FindAsync(string hash, AccountTokenPurpose purpose, CancellationToken cancellationToken);
}
