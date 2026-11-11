using Mazwi.Domain;

namespace Mazwi.Repository.Tests;

public sealed class ArtistRepositoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static Artist NewArtist(string? email = null) => Artist.Rehydrate(Guid.NewGuid(),
        new EmailAddress(email ?? $"repo-{Guid.NewGuid():N}@mazwi.test"), new PasswordHash("hash'; DROP TABLE artists;--"), DateTimeOffset.UtcNow);

    [Fact]
    public async Task AddAsync_NewArtist_RoundTripsByIdAndEmail()
    {
        // Arrange
        var repository = new ArtistRepository(fixture.DataSource);
        Artist artist = NewArtist();
        try
        {
            // Act
            Assert.True(await repository.AddAsync(artist, CancellationToken.None));
            Artist? byId = await repository.GetAsync(artist.Id, CancellationToken.None);
            Artist? byEmail = await repository.FindByEmailAsync(artist.Email, CancellationToken.None);
            // Assert
            Assert.Equal(artist.Email, byId?.Email);
            Assert.Equal(artist.PasswordHash, byId?.PasswordHash);
            Assert.Equal(artist.Id, byEmail?.Id);
        }
        finally
        {
            await repository.DeleteAsync(artist.Id, CancellationToken.None);
        }
    }

    [Fact]
    public async Task AddAsync_TakenEmail_ReturnsFalseAndKeepsFirstAccount()
    {
        // Arrange
        var repository = new ArtistRepository(fixture.DataSource);
        Artist first = NewArtist();
        Artist second = NewArtist(first.Email.Value.ToUpperInvariant());
        try
        {
            // Act
            Assert.True(await repository.AddAsync(first, CancellationToken.None));
            bool added = await repository.AddAsync(second, CancellationToken.None);
            // Assert
            Assert.False(added);
            Assert.Equal(first.Id, (await repository.FindByEmailAsync(first.Email, CancellationToken.None))?.Id);
            Assert.Null(await repository.GetAsync(second.Id, CancellationToken.None));
        }
        finally
        {
            await repository.DeleteAsync(first.Id, CancellationToken.None);
        }
    }

    [Fact]
    public async Task GetAndFind_MissingArtist_ReturnNull()
    {
        // Arrange
        var repository = new ArtistRepository(fixture.DataSource);
        // Act / Assert
        Assert.Null(await repository.GetAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Null(await repository.FindByEmailAsync(new EmailAddress($"missing-{Guid.NewGuid():N}@mazwi.test"), CancellationToken.None));
    }

    [Fact]
    public async Task AddAsync_Cancelled_Throws()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        // Act / Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ArtistRepository(fixture.DataSource).AddAsync(NewArtist(), cancellation.Token));
    }
}
