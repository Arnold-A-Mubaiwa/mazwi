using Mazwi.Domain;

namespace Mazwi.Repository.Tests;

public sealed class AccountTokenRepositoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private async Task<Artist> ArtistAsync()
    {
        Artist artist = Artist.Rehydrate(Guid.NewGuid(), new EmailAddress($"tokens-{Guid.NewGuid():N}@mazwi.test"), new PasswordHash("hash"), Now);
        await new ArtistRepository(fixture.DataSource).AddAsync(artist, CancellationToken.None);
        return artist;
    }

    [Fact]
    public async Task ReplaceAsync_NewLink_FindsItAndRetiresTheEarlierUnusedLink()
    {
        // Arrange
        var tokens = new AccountTokenRepository(fixture.DataSource);
        Artist artist = await ArtistAsync();
        try
        {
            (AccountToken first, _) = AccountToken.Issue(artist.Id, AccountTokenPurpose.PasswordReset, Now);
            (AccountToken second, _) = AccountToken.Issue(artist.Id, AccountTokenPurpose.PasswordReset, Now);
            (AccountToken verification, _) = AccountToken.Issue(artist.Id, AccountTokenPurpose.EmailVerification, Now);
            // Act
            await tokens.ReplaceAsync(first, CancellationToken.None);
            await tokens.ReplaceAsync(verification, CancellationToken.None);
            await tokens.ReplaceAsync(second, CancellationToken.None);
            // Assert
            Assert.Null(await tokens.FindAsync(first.Hash, AccountTokenPurpose.PasswordReset, CancellationToken.None));
            AccountToken? found = await tokens.FindAsync(second.Hash, AccountTokenPurpose.PasswordReset, CancellationToken.None);
            Assert.Equal(second.ExpiresAt.ToUnixTimeMilliseconds(), found!.ExpiresAt.ToUnixTimeMilliseconds());
            Assert.Equal(artist.Id, found.ArtistId);
            Assert.NotNull(await tokens.FindAsync(verification.Hash, AccountTokenPurpose.EmailVerification, CancellationToken.None));
            Assert.Null(await tokens.FindAsync(verification.Hash, AccountTokenPurpose.PasswordReset, CancellationToken.None));
        }
        finally { await new ArtistRepository(fixture.DataSource).DeleteAsync(artist.Id, CancellationToken.None); }
    }

    [Fact]
    public async Task SaveSecurityAsync_UsedLink_SavesPasswordStampVerificationAndRefusesSecondUse()
    {
        // Arrange
        var artists = new ArtistRepository(fixture.DataSource);
        var tokens = new AccountTokenRepository(fixture.DataSource);
        Artist artist = await ArtistAsync();
        try
        {
            (AccountToken token, _) = AccountToken.Issue(artist.Id, AccountTokenPurpose.PasswordReset, Now);
            await tokens.ReplaceAsync(token, CancellationToken.None);
            Artist loaded = (await artists.GetAsync(artist.Id, CancellationToken.None))!;
            Artist again = (await artists.GetAsync(artist.Id, CancellationToken.None))!;
            AccountToken copy = (await tokens.FindAsync(token.Hash, AccountTokenPurpose.PasswordReset, CancellationToken.None))!;
            token.Use(Now);
            copy.Use(Now);
            loaded.ChangePassword(new NewPassword("brand new password"), new FakeHasher());
            loaded.VerifyEmail(Now);
            // Act
            await artists.SaveSecurityAsync(loaded, token, CancellationToken.None);
            Artist saved = (await artists.GetAsync(artist.Id, CancellationToken.None))!;
            // Assert
            Assert.Equal("new", saved.PasswordHash.Value);
            Assert.Equal(loaded.SecurityStamp, saved.SecurityStamp);
            Assert.NotEqual(artist.SecurityStamp, saved.SecurityStamp);
            Assert.True(saved.EmailVerified);
            Assert.NotNull((await tokens.FindAsync(token.Hash, AccountTokenPurpose.PasswordReset, CancellationToken.None))!.UsedAt);
            await Assert.ThrowsAsync<DomainException>(() => artists.SaveSecurityAsync(again, copy, CancellationToken.None));
        }
        finally { await artists.DeleteAsync(artist.Id, CancellationToken.None); }
    }

    [Fact]
    public async Task DeleteArtist_Tokens_AreRemovedWithTheAccount()
    {
        // Arrange
        var tokens = new AccountTokenRepository(fixture.DataSource);
        Artist artist = await ArtistAsync();
        (AccountToken token, _) = AccountToken.Issue(artist.Id, AccountTokenPurpose.EmailVerification, Now);
        await tokens.ReplaceAsync(token, CancellationToken.None);
        // Act
        await new ArtistRepository(fixture.DataSource).DeleteAsync(artist.Id, CancellationToken.None);
        // Assert
        Assert.Null(await tokens.FindAsync(token.Hash, AccountTokenPurpose.EmailVerification, CancellationToken.None));
    }

    [Fact]
    public async Task LocalOutbox_Message_WritesAnEmlFileWithHeadersAndBody()
    {
        // Arrange
        string root = Path.Combine(Path.GetTempPath(), "mazwi-outbox-tests", Guid.NewGuid().ToString("N"));
        try
        {
            // Act
            await new LocalOutboxEmailSender(root).SendAsync(new EmailMessage(new EmailAddress("rudo@example.com"), "Reset your Mazwi password", "Line one\nLine two"), CancellationToken.None);
            // Assert
            string content = await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(root, "*.eml")));
            Assert.Contains("To: rudo@example.com\r\n", content, StringComparison.Ordinal);
            Assert.Contains("Subject: Reset your Mazwi password\r\n", content, StringComparison.Ordinal);
            Assert.EndsWith("\r\n\r\nLine one\r\nLine two\r\n", content, StringComparison.Ordinal);
        }
        finally { if (Directory.Exists(root)) { Directory.Delete(root, recursive: true); } }
    }

    private sealed class FakeHasher : IPasswordHasher
    {
        public PasswordHash Hash(NewPassword password) => new("new");
        public bool Verify(PasswordHash hash, string attempt) => false;
    }
}
