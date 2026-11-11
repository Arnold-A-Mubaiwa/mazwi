using Mazwi.Domain;
using NSubstitute;

namespace Mazwi.Application.Tests;

public sealed class ArtistAccountServiceTests
{
    private readonly IArtistRepository _artists = Substitute.For<IArtistRepository>();
    private readonly IStoryRepository _stories = Substitute.For<IStoryRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ArtistAccountService _service;

    public ArtistAccountServiceTests()
    {
        _hasher.Hash(Arg.Any<NewPassword>()).Returns(call => new PasswordHash($"hashed:{call.Arg<NewPassword>().Value}"));
        _hasher.Verify(Arg.Any<PasswordHash>(), Arg.Any<string>()).Returns(call => call.Arg<PasswordHash>().Value == $"hashed:{call.Arg<string>()}");
        _service = new ArtistAccountService(_artists, _stories, _hasher);
    }

    private Artist Existing() => Artist.Register(new EmailAddress("rudo@example.com"), new NewPassword("long password"), _hasher);

    [Fact]
    public async Task SignUpAsync_NewEmail_SavesHashedArtistAndPassesCancellation()
    {
        // Arrange
        _artists.AddAsync(Arg.Any<Artist>(), Arg.Any<CancellationToken>()).Returns(true);
        using var cancellation = new CancellationTokenSource();
        // Act
        ArtistDto artist = await _service.SignUpAsync(" Rudo@Example.com ", "long password", cancellation.Token);
        // Assert
        Assert.Equal("rudo@example.com", artist.Email);
        await _artists.Received(1).AddAsync(Arg.Is<Artist>(a => a.Id == artist.Id && a.PasswordHash.Value == "hashed:long password"), cancellation.Token);
    }

    [Fact]
    public async Task SignUpAsync_TakenEmail_ThrowsAccountExists()
    {
        // Arrange
        _artists.AddAsync(Arg.Any<Artist>(), Arg.Any<CancellationToken>()).Returns(false);
        // Act / Assert
        await Assert.ThrowsAsync<AccountExistsException>(() => _service.SignUpAsync("rudo@example.com", "long password", CancellationToken.None));
    }

    [Theory]
    [InlineData("not-an-email", "long password")]
    [InlineData("rudo@example.com", "short")]
    public async Task SignUpAsync_InvalidInput_ThrowsWithoutSaving(string email, string password)
    {
        // Act / Assert
        await Assert.ThrowsAsync<DomainException>(() => _service.SignUpAsync(email, password, CancellationToken.None));
        await _artists.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task SignInAsync_CorrectPassword_ReturnsArtist()
    {
        // Arrange
        Artist artist = Existing();
        _artists.FindByEmailAsync(new EmailAddress("rudo@example.com"), Arg.Any<CancellationToken>()).Returns(artist);
        // Act
        ArtistDto? result = await _service.SignInAsync("RUDO@example.com", "long password", CancellationToken.None);
        // Assert
        Assert.Equal(artist.Id, result?.Id);
    }

    [Theory]
    [InlineData("rudo@example.com", "wrong password")]
    [InlineData("other@example.com", "long password")]
    [InlineData("not-an-email", "long password")]
    [InlineData("rudo@example.com", "")]
    public async Task SignInAsync_WrongCredentials_ReturnsNull(string email, string password)
    {
        // Arrange
        Artist artist = Existing();
        _artists.FindByEmailAsync(new EmailAddress("rudo@example.com"), Arg.Any<CancellationToken>()).Returns(artist);
        // Act / Assert
        Assert.Null(await _service.SignInAsync(email, password, CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_KnownAndUnknownArtist_ReturnsArtistOrNull()
    {
        // Arrange
        Artist artist = Existing();
        _artists.GetAsync(artist.Id, Arg.Any<CancellationToken>()).Returns(artist);
        // Act / Assert
        Assert.Equal("rudo@example.com", (await _service.GetAsync(artist.Id, CancellationToken.None))?.Email);
        Assert.Null(await _service.GetAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task ClaimUnownedStoriesAsync_KnownArtist_ClaimsForThatArtist()
    {
        // Arrange
        Artist artist = Existing();
        _artists.FindByEmailAsync(artist.Email, Arg.Any<CancellationToken>()).Returns(artist);
        _stories.ClaimUnownedAsync(artist.Id, Arg.Any<CancellationToken>()).Returns(3);
        // Act / Assert
        Assert.Equal(3, await _service.ClaimUnownedStoriesAsync("rudo@example.com", CancellationToken.None));
    }

    [Fact]
    public async Task ClaimUnownedStoriesAsync_UnknownEmail_ThrowsWithoutClaiming()
    {
        // Act / Assert
        await Assert.ThrowsAsync<DomainException>(() => _service.ClaimUnownedStoriesAsync("nobody@example.com", CancellationToken.None));
        await _stories.DidNotReceiveWithAnyArgs().ClaimUnownedAsync(default, default);
    }
}
