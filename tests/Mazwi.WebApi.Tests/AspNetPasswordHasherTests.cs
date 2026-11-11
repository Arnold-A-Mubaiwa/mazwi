using Mazwi.Domain;

namespace Mazwi.WebApi.Tests;

public sealed class AspNetPasswordHasherTests
{
    private readonly AspNetPasswordHasher _hasher = new();

    [Fact]
    public void HashAndVerify_SamePassword_MatchesWithoutStoringPlainText()
    {
        // Act
        PasswordHash hash = _hasher.Hash(new NewPassword("long password"));
        // Assert
        Assert.DoesNotContain("long password", hash.Value, StringComparison.Ordinal);
        Assert.True(_hasher.Verify(hash, "long password"));
        Assert.NotEqual(hash, _hasher.Hash(new NewPassword("long password")));
    }

    [Theory]
    [InlineData("wrong password")]
    [InlineData("")]
    public void Verify_WrongPassword_ReturnsFalse(string attempt)
    {
        // Arrange
        PasswordHash hash = _hasher.Hash(new NewPassword("long password"));
        // Act / Assert
        Assert.False(_hasher.Verify(hash, attempt));
    }

    [Theory]
    [InlineData("fixture-account-cannot-sign-in")]
    [InlineData("AAAAAAAAAAAAAAAAAA==")]
    public void Verify_MalformedStoredHash_ReturnsFalseInsteadOfThrowing(string stored)
    {
        // Act / Assert
        Assert.False(_hasher.Verify(new PasswordHash(stored), "long password"));
    }

    [Fact]
    public void HashAndVerify_NullArguments_Throw()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => _hasher.Hash(null!));
        Assert.Throws<ArgumentNullException>(() => _hasher.Verify(null!, "long password"));
    }
}
