namespace Mazwi.Domain.Tests;

public sealed class AccountTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Issue_PasswordReset_StoresOnlyTheHashAndExpiresInOneHour()
    {
        // Act
        (AccountToken token, string secret) = AccountToken.Issue(TestOwner.Id, AccountTokenPurpose.PasswordReset, Now);
        // Assert
        Assert.Equal(64, token.Hash.Length);
        Assert.Equal(AccountToken.HashOf(secret), token.Hash);
        Assert.DoesNotContain(secret, token.Hash, StringComparison.Ordinal);
        Assert.True(secret.Length >= 43 && secret.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'));
        Assert.Equal(Now.AddHours(1), token.ExpiresAt);
        Assert.NotEqual(secret, AccountToken.Issue(TestOwner.Id, AccountTokenPurpose.PasswordReset, Now).Secret);
    }

    [Fact]
    public void Issue_EmailVerification_ExpiresInThreeDays()
    {
        // Act / Assert
        Assert.Equal(Now.AddDays(3), AccountToken.Issue(TestOwner.Id, AccountTokenPurpose.EmailVerification, Now).Token.ExpiresAt);
    }

    [Fact]
    public void IsUsable_Boundaries_ValidUntilJustBeforeExpiry()
    {
        // Arrange
        AccountToken token = AccountToken.Issue(TestOwner.Id, AccountTokenPurpose.PasswordReset, Now).Token;
        // Act / Assert
        Assert.True(token.IsUsable(Now));
        Assert.True(token.IsUsable(token.ExpiresAt.AddTicks(-1)));
        Assert.False(token.IsUsable(token.ExpiresAt));
    }

    [Fact]
    public void Use_Twice_SecondUseThrows()
    {
        // Arrange
        AccountToken token = AccountToken.Issue(TestOwner.Id, AccountTokenPurpose.PasswordReset, Now).Token;
        // Act
        token.Use(Now);
        // Assert
        Assert.Equal(Now, token.UsedAt);
        Assert.False(token.IsUsable(Now));
        Assert.Throws<DomainException>(() => token.Use(Now));
    }

    [Fact]
    public void Use_Expired_Throws()
    {
        // Arrange
        AccountToken token = AccountToken.Issue(TestOwner.Id, AccountTokenPurpose.PasswordReset, Now).Token;
        // Act / Assert
        Assert.Throws<DomainException>(() => token.Use(Now.AddHours(2)));
    }

    [Fact]
    public void Rehydrate_InvalidParts_Throws()
    {
        // Arrange
        string hash = AccountToken.HashOf("secret");
        // Act / Assert
        Assert.Throws<DomainException>(() => AccountToken.Rehydrate(Guid.Empty, TestOwner.Id, AccountTokenPurpose.PasswordReset, hash, Now, null));
        Assert.Throws<DomainException>(() => AccountToken.Rehydrate(Guid.NewGuid(), Guid.Empty, AccountTokenPurpose.PasswordReset, hash, Now, null));
        Assert.Throws<DomainException>(() => AccountToken.Rehydrate(Guid.NewGuid(), TestOwner.Id, (AccountTokenPurpose)9, hash, Now, null));
        Assert.Throws<DomainException>(() => AccountToken.Rehydrate(Guid.NewGuid(), TestOwner.Id, AccountTokenPurpose.PasswordReset, "short", Now, null));
        Assert.Throws<DomainException>(() => AccountToken.Rehydrate(Guid.NewGuid(), TestOwner.Id, AccountTokenPurpose.PasswordReset, hash, default, null));
    }

    [Fact]
    public void HashOf_SameSecret_IsStableLowerHex()
    {
        // Act / Assert
        Assert.Equal(AccountToken.HashOf("abc"), AccountToken.HashOf("abc"));
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", AccountToken.HashOf("abc"));
        Assert.Equal(AccountToken.HashOf(""), AccountToken.HashOf(null!));
    }

    private sealed class FakeHasher : IPasswordHasher
    {
        public PasswordHash Hash(NewPassword password) => new($"hashed:{password.Value}");
        public bool Verify(PasswordHash hash, string attempt) => hash.Value == $"hashed:{attempt}";
    }

    [Fact]
    public void ChangePassword_NewPassword_ChangesHashAndSecurityStamp()
    {
        // Arrange
        Artist artist = Artist.Register(new EmailAddress("rudo@example.com"), new NewPassword("old password"), new FakeHasher());
        Guid stamp = artist.SecurityStamp;
        // Act
        artist.ChangePassword(new NewPassword("new password"), new FakeHasher());
        // Assert
        Assert.True(artist.CanSignIn("new password", new FakeHasher()));
        Assert.False(artist.CanSignIn("old password", new FakeHasher()));
        Assert.NotEqual(stamp, artist.SecurityStamp);
        Assert.Throws<ArgumentNullException>(() => artist.ChangePassword(null!, new FakeHasher()));
    }

    [Fact]
    public void VerifyEmail_FirstTimeOnly_KeepsTheFirstTime()
    {
        // Arrange
        Artist artist = Artist.Register(new EmailAddress("rudo@example.com"), new NewPassword("old password"), new FakeHasher());
        // Act
        artist.VerifyEmail(Now);
        artist.VerifyEmail(Now.AddDays(1));
        // Assert
        Assert.True(artist.EmailVerified);
        Assert.Equal(Now, artist.EmailVerifiedAt);
        Assert.Throws<DomainException>(() => artist.VerifyEmail(default));
        Assert.Throws<DomainException>(() => Artist.Rehydrate(Guid.NewGuid(), artist.Email, artist.PasswordHash, Now, securityStamp: Guid.Empty));
    }

    [Theory]
    [InlineData("Hello\r\nBcc: someone@example.com")]
    [InlineData("")]
    public void EmailMessage_InvalidSubject_Throws(string subject)
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => new EmailMessage(new EmailAddress("rudo@example.com"), subject, "Body"));
    }

    [Fact]
    public void EmailMessage_Valid_KeepsParts()
    {
        // Act
        var message = new EmailMessage(new EmailAddress("rudo@example.com"), " Subject ", "Line one\nLine two");
        // Assert
        Assert.Equal("Subject", message.Subject);
        Assert.Throws<DomainException>(() => new EmailMessage(message.To, "Subject", " "));
    }
}
