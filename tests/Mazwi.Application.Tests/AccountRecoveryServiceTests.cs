using System.Text.RegularExpressions;
using Mazwi.Domain;
using NSubstitute;

namespace Mazwi.Application.Tests;

public sealed partial class AccountRecoveryServiceTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private readonly IArtistRepository _artists = Substitute.For<IArtistRepository>();
    private readonly IAccountTokenRepository _tokens = Substitute.For<IAccountTokenRepository>();
    private readonly IEmailSender _email = Substitute.For<IEmailSender>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly Clock _clock = new();
    private readonly AccountRecoveryService _service;
    private readonly Artist _artist;
    private readonly List<EmailMessage> _sent = [];
    private readonly List<AccountToken> _stored = [];

    public AccountRecoveryServiceTests()
    {
        _hasher.Hash(Arg.Any<NewPassword>()).Returns(call => new PasswordHash($"hashed:{call.Arg<NewPassword>().Value}"));
        _artist = Artist.Register(new EmailAddress("rudo@example.com"), new NewPassword("old password"), _hasher);
        _artists.FindByEmailAsync(_artist.Email, Arg.Any<CancellationToken>()).Returns(_artist);
        _artists.GetAsync(_artist.Id, Arg.Any<CancellationToken>()).Returns(_artist);
        _email.SendAsync(Arg.Do<EmailMessage>(_sent.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _tokens.ReplaceAsync(Arg.Do<AccountToken>(_stored.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _tokens.FindAsync(Arg.Any<string>(), Arg.Any<AccountTokenPurpose>(), Arg.Any<CancellationToken>())
            .Returns(call => _stored.SingleOrDefault(token => token.Hash == call.ArgAt<string>(0) && token.Purpose == call.ArgAt<AccountTokenPurpose>(1)));
        _service = new AccountRecoveryService(_artists, _tokens, _email, _hasher, _clock, new AccountLinks("https://mazwi.example"));
    }

    [GeneratedRegex(@"token=([A-Za-z0-9_\-]+)")]
    private static partial Regex TokenPattern();

    private string SecretFromEmail() => TokenPattern().Match(_sent.Last().Body).Groups[1].Value;

    [Fact]
    public async Task RequestPasswordResetAsync_KnownEmail_EmailsALinkToTheConfiguredSite()
    {
        // Act
        await _service.RequestPasswordResetAsync("RUDO@example.com", CancellationToken.None);
        // Assert
        EmailMessage message = Assert.Single(_sent);
        Assert.Equal(_artist.Email, message.To);
        Assert.Contains("https://mazwi.example/reset-password?token=", message.Body, StringComparison.Ordinal);
        Assert.Equal(AccountToken.HashOf(SecretFromEmail()), Assert.Single(_stored).Hash);
    }

    [Theory]
    [InlineData("nobody@example.com")]
    [InlineData("not-an-email")]
    public async Task RequestPasswordResetAsync_UnknownOrInvalidEmail_SendsNothingAndDoesNotThrow(string email)
    {
        // Act
        await _service.RequestPasswordResetAsync(email, CancellationToken.None);
        // Assert
        Assert.Empty(_sent);
        await _tokens.DidNotReceiveWithAnyArgs().ReplaceAsync(default!, default);
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidLink_ChangesPasswordStampAndVerifiesEmail()
    {
        // Arrange
        await _service.RequestPasswordResetAsync("rudo@example.com", CancellationToken.None);
        Guid stamp = _artist.SecurityStamp;
        // Act
        bool reset = await _service.ResetPasswordAsync(SecretFromEmail(), "brand new password", CancellationToken.None);
        // Assert
        Assert.True(reset);
        Assert.Equal("hashed:brand new password", _artist.PasswordHash.Value);
        Assert.NotEqual(stamp, _artist.SecurityStamp);
        Assert.True(_artist.EmailVerified);
        await _artists.Received(1).SaveSecurityAsync(_artist, Arg.Is<AccountToken>(token => token.UsedAt == _clock.Now), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetPasswordAsync_ExpiredUsedOrWrongLink_ReturnsFalseWithoutSaving()
    {
        // Arrange
        await _service.RequestPasswordResetAsync("rudo@example.com", CancellationToken.None);
        string secret = SecretFromEmail();
        // Act / Assert
        Assert.False(await _service.ResetPasswordAsync("wrong-secret", "brand new password", CancellationToken.None));
        Assert.False(await _service.ResetPasswordAsync("", "brand new password", CancellationToken.None));
        Assert.False(await _service.ResetPasswordAsync(new string('a', 201), "brand new password", CancellationToken.None));
        _clock.Now = _clock.Now.AddHours(1);
        Assert.False(await _service.ResetPasswordAsync(secret, "brand new password", CancellationToken.None));
        await _artists.DidNotReceiveWithAnyArgs().SaveSecurityAsync(default!, default, default);
    }

    [Fact]
    public async Task ResetPasswordAsync_LinkAlreadyUsed_ReturnsFalse()
    {
        // Arrange
        await _service.RequestPasswordResetAsync("rudo@example.com", CancellationToken.None);
        string secret = SecretFromEmail();
        await _service.ResetPasswordAsync(secret, "brand new password", CancellationToken.None);
        // Act / Assert
        Assert.False(await _service.ResetPasswordAsync(secret, "another new password", CancellationToken.None));
    }

    [Fact]
    public async Task ResetPasswordAsync_WeakPassword_ThrowsBeforeUsingTheLink()
    {
        // Arrange
        await _service.RequestPasswordResetAsync("rudo@example.com", CancellationToken.None);
        // Act / Assert
        await Assert.ThrowsAsync<DomainException>(() => _service.ResetPasswordAsync(SecretFromEmail(), "short", CancellationToken.None));
        Assert.Null(Assert.Single(_stored).UsedAt);
    }

    [Fact]
    public async Task VerifyEmail_SentLink_MarksVerifiedAndAlreadyVerifiedSendsNothing()
    {
        // Act
        Assert.True(await _service.SendVerificationAsync(_artist.Id, CancellationToken.None));
        Assert.Contains("https://mazwi.example/verify-email?token=", _sent.Single().Body, StringComparison.Ordinal);
        Assert.False(await _service.ResetPasswordAsync(SecretFromEmail(), "brand new password", CancellationToken.None));
        bool verified = await _service.VerifyEmailAsync(SecretFromEmail(), CancellationToken.None);
        // Assert
        Assert.True(verified);
        Assert.True(_artist.EmailVerified);
        Assert.False(await _service.SendVerificationAsync(_artist.Id, CancellationToken.None));
        Assert.False(await _service.SendVerificationAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Single(_sent);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://mazwi.example")]
    [InlineData("https://user:pass@mazwi.example")]
    public void AccountLinks_InvalidBase_Throws(string value)
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => new AccountLinks(value));
    }

    [Fact]
    public void AccountLinks_For_EscapesTheSecret()
    {
        // Act / Assert
        Assert.Equal("https://mazwi.example/reset-password?token=a%2Bb%26c", new AccountLinks("https://mazwi.example/").For("/reset-password", "a+b&c"));
    }
}
