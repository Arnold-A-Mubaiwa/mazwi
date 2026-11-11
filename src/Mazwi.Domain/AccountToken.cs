using System.Security.Cryptography;
using System.Text;

namespace Mazwi.Domain;

public enum AccountTokenPurpose
{
    PasswordReset,
    EmailVerification,
}

// A single-use link sent by email. Only a SHA-256 hash of the secret is kept, so a database copy cannot be used to reset passwords.
public sealed class AccountToken
{
    public static readonly TimeSpan PasswordResetLifetime = TimeSpan.FromHours(1);
    public static readonly TimeSpan EmailVerificationLifetime = TimeSpan.FromDays(3);

    public Guid Id { get; }
    public Guid ArtistId { get; }
    public AccountTokenPurpose Purpose { get; }
    public string Hash { get; }
    public DateTimeOffset ExpiresAt { get; }
    public DateTimeOffset? UsedAt { get; private set; }

    private AccountToken(Guid id, Guid artistId, AccountTokenPurpose purpose, string hash, DateTimeOffset expiresAt, DateTimeOffset? usedAt)
    {
        if (id == Guid.Empty || artistId == Guid.Empty || !Enum.IsDefined(purpose) || hash is not { Length: 64 } || expiresAt == default)
        {
            throw new DomainException("An account token needs an identity, an artist, a purpose, a hash and an expiry.");
        }

        Id = id;
        ArtistId = artistId;
        Purpose = purpose;
        Hash = hash;
        ExpiresAt = expiresAt;
        UsedAt = usedAt;
    }

    // Returns the token to store and the secret to put in the emailed link (never stored).
    public static (AccountToken Token, string Secret) Issue(Guid artistId, AccountTokenPurpose purpose, DateTimeOffset now)
    {
        string secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        TimeSpan lifetime = purpose == AccountTokenPurpose.PasswordReset ? PasswordResetLifetime : EmailVerificationLifetime;
        return (new AccountToken(Guid.NewGuid(), artistId, purpose, HashOf(secret), now + lifetime, null), secret);
    }

    public static string HashOf(string secret) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret ?? string.Empty)));

    public bool IsUsable(DateTimeOffset now) => UsedAt is null && now < ExpiresAt;

    public void Use(DateTimeOffset now)
    {
        if (!IsUsable(now))
        {
            throw new DomainException("This link has expired or was already used. Request a new one.");
        }

        UsedAt = now;
    }

    public static AccountToken Rehydrate(Guid id, Guid artistId, AccountTokenPurpose purpose, string hash, DateTimeOffset expiresAt, DateTimeOffset? usedAt) =>
        new(id, artistId, purpose, hash, expiresAt, usedAt);
}
