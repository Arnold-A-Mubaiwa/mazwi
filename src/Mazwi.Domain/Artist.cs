namespace Mazwi.Domain;

public sealed class Artist
{
    public Guid Id { get; }
    public EmailAddress Email { get; }
    public PasswordHash PasswordHash { get; }
    public DateTimeOffset CreatedAt { get; }

    private Artist(Guid id, EmailAddress email, PasswordHash passwordHash, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || createdAt == default)
        {
            throw new DomainException("An artist requires an identity and a creation time.");
        }

        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(passwordHash);
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        CreatedAt = createdAt;
    }

    public static Artist Register(EmailAddress email, NewPassword password, IPasswordHasher hasher)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(hasher);
        return new Artist(Guid.NewGuid(), email, hasher.Hash(password), DateTimeOffset.UtcNow);
    }

    public bool CanSignIn(string attempt, IPasswordHasher hasher)
    {
        ArgumentNullException.ThrowIfNull(hasher);
        return !string.IsNullOrEmpty(attempt) && attempt.Length <= NewPassword.MaximumLength && hasher.Verify(PasswordHash, attempt);
    }

    public static Artist Rehydrate(Guid id, EmailAddress email, PasswordHash passwordHash, DateTimeOffset createdAt) =>
        new(id, email, passwordHash, createdAt);
}
