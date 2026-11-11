namespace Mazwi.Domain.Tests;

public sealed class ArtistTests
{
    private sealed class FakeHasher : IPasswordHasher
    {
        public PasswordHash Hash(NewPassword password) => new($"hashed:{password.Value}");
        public bool Verify(PasswordHash hash, string attempt) => hash.Value == $"hashed:{attempt}";
    }

    [Theory]
    [InlineData("  Rudo@Example.COM ", "rudo@example.com")]
    [InlineData("a@b.co", "a@b.co")]
    public void EmailAddress_ValidInput_TrimsAndLowerCases(string input, string expected)
    {
        // Act
        var email = new EmailAddress(input);
        // Assert
        Assert.Equal(expected, email.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-at-sign.com")]
    [InlineData("@example.com")]
    [InlineData("rudo@")]
    [InlineData("rudo@example")]
    [InlineData("rudo@example.com.")]
    [InlineData("ru do@example.com")]
    [InlineData("a@b@example.com")]
    [InlineData(null)]
    public void EmailAddress_InvalidInput_Throws(string? input)
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => new EmailAddress(input!));
    }

    [Fact]
    public void EmailAddress_LongerThanMaximum_Throws()
    {
        // Arrange
        string email = new string('a', EmailAddress.MaximumLength - 11) + "@example.com";
        // Act / Assert
        Assert.Equal(EmailAddress.MaximumLength + 1, email.Length);
        Assert.Throws<DomainException>(() => new EmailAddress(email));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(128)]
    public void NewPassword_BoundaryLengths_AreAccepted(int length)
    {
        // Act
        var password = new NewPassword(new string('p', length));
        // Assert
        Assert.Equal(length, password.Value.Length);
        Assert.DoesNotContain("p", password.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(129)]
    public void NewPassword_OutsideLengths_Throws(int length)
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => new NewPassword(new string('p', length)));
    }

    [Fact]
    public void NewPassword_OnlyWhitespaceOrNull_Throws()
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => new NewPassword(new string(' ', 12)));
        Assert.Throws<DomainException>(() => new NewPassword(null!));
    }

    [Fact]
    public void PasswordHash_Empty_ThrowsAndToStringHidesValue()
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => new PasswordHash(" "));
        Assert.DoesNotContain("secret", new PasswordHash("secret-hash").ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Register_ValidInput_HashesPasswordAndCreatesIdentity()
    {
        // Act
        Artist artist = Artist.Register(new EmailAddress("rudo@example.com"), new NewPassword("long password"), new FakeHasher());
        // Assert
        Assert.NotEqual(Guid.Empty, artist.Id);
        Assert.Equal("hashed:long password", artist.PasswordHash.Value);
        Assert.NotEqual(default, artist.CreatedAt);
    }

    [Fact]
    public void Register_NullArguments_Throw()
    {
        // Arrange
        var email = new EmailAddress("rudo@example.com");
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => Artist.Register(email, null!, new FakeHasher()));
        Assert.Throws<ArgumentNullException>(() => Artist.Register(email, new NewPassword("long password"), null!));
        Assert.Throws<ArgumentNullException>(() => Artist.Register(null!, new NewPassword("long password"), new FakeHasher()));
    }

    [Theory]
    [InlineData("long password", true)]
    [InlineData("wrong password", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void CanSignIn_Attempt_MatchesOnlyCorrectPassword(string? attempt, bool expected)
    {
        // Arrange
        Artist artist = Artist.Register(new EmailAddress("rudo@example.com"), new NewPassword("long password"), new FakeHasher());
        // Act / Assert
        Assert.Equal(expected, artist.CanSignIn(attempt!, new FakeHasher()));
    }

    [Fact]
    public void CanSignIn_OverlongAttempt_ReturnsFalseWithoutHashing()
    {
        // Arrange
        string overlong = new('p', NewPassword.MaximumLength + 1);
        Artist artist = Artist.Rehydrate(Guid.NewGuid(), new EmailAddress("rudo@example.com"), new PasswordHash($"hashed:{overlong}"), DateTimeOffset.UtcNow);
        // Act / Assert
        Assert.False(artist.CanSignIn(overlong, new FakeHasher()));
    }

    [Fact]
    public void Rehydrate_MissingIdentityOrTime_Throws()
    {
        // Arrange
        var email = new EmailAddress("rudo@example.com");
        var hash = new PasswordHash("hash");
        // Act / Assert
        Assert.Throws<DomainException>(() => Artist.Rehydrate(Guid.Empty, email, hash, DateTimeOffset.UtcNow));
        Assert.Throws<DomainException>(() => Artist.Rehydrate(Guid.NewGuid(), email, hash, default));
    }
}
