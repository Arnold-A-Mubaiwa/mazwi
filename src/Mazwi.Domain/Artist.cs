namespace Mazwi.Domain;

public sealed class Artist
{
    public Guid Id { get; }
    public EmailAddress Email { get; }
    public PasswordHash PasswordHash { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? EmailVerifiedAt { get; private set; }
    // Changes whenever the password changes, so sessions signed in with the old password stop working.
    public Guid SecurityStamp { get; private set; }
    public bool EmailVerified => EmailVerifiedAt is not null;

    private Artist(Guid id, EmailAddress email, PasswordHash passwordHash, DateTimeOffset createdAt, DateTimeOffset? emailVerifiedAt, Guid securityStamp)
    {
        if (id == Guid.Empty || createdAt == default || securityStamp == Guid.Empty)
        {
            throw new DomainException("An artist requires an identity, a creation time and a security stamp.");
        }

        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(passwordHash);
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        CreatedAt = createdAt;
        EmailVerifiedAt = emailVerifiedAt;
        SecurityStamp = securityStamp;
    }

    public static Artist Register(EmailAddress email, NewPassword password, IPasswordHasher hasher)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(hasher);
        return new Artist(Guid.NewGuid(), email, hasher.Hash(password), DateTimeOffset.UtcNow, null, Guid.NewGuid());
    }

    public bool CanSignIn(string attempt, IPasswordHasher hasher)
    {
        ArgumentNullException.ThrowIfNull(hasher);
        return !string.IsNullOrEmpty(attempt) && attempt.Length <= NewPassword.MaximumLength && hasher.Verify(PasswordHash, attempt);
    }

    public void ChangePassword(NewPassword password, IPasswordHasher hasher)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(hasher);
        PasswordHash = hasher.Hash(password);
        SecurityStamp = Guid.NewGuid();
    }

    public void VerifyEmail(DateTimeOffset at)
    {
        if (at == default)
        {
            throw new DomainException("A verification time is required.");
        }

        EmailVerifiedAt ??= at;
    }

    public static Artist Rehydrate(Guid id, EmailAddress email, PasswordHash passwordHash, DateTimeOffset createdAt,
        DateTimeOffset? emailVerifiedAt = null, Guid? securityStamp = null) =>
        new(id, email, passwordHash, createdAt, emailVerifiedAt, securityStamp ?? Guid.NewGuid());
}
