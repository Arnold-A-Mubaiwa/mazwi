using Mazwi.Domain;

namespace Mazwi.Repository.Tests;

public sealed class StoryOwnershipRepositoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly StoryDetails Details = new("Ownership fixture", "Test artist", "Shona", "ZW", "story", "sn");

    private async Task<Artist> AddArtistAsync()
    {
        Artist artist = Artist.Rehydrate(Guid.NewGuid(), new EmailAddress($"owner-{Guid.NewGuid():N}@mazwi.test"), new PasswordHash("hash"), DateTimeOffset.UtcNow);
        await new ArtistRepository(fixture.DataSource).AddAsync(artist, CancellationToken.None);
        return artist;
    }

    [Fact]
    public async Task AddAsync_OwnedStory_RoundTripsOwnerAndListsOnlyForOwner()
    {
        // Arrange
        var stories = new StoryRepository(fixture.DataSource);
        var artists = new ArtistRepository(fixture.DataSource);
        Artist owner = await AddArtistAsync();
        Artist other = await AddArtistAsync();
        Story story = Story.Upload(Details, new StoryRecording("clip.wav", 12), true, owner.Id);
        try
        {
            // Act
            await stories.AddAsync(story, CancellationToken.None);
            Story? loaded = await stories.GetAsync(story.Id, CancellationToken.None);
            IReadOnlyList<Guid> owned = await stories.ListOwnedIdsAsync(owner.Id, 0, 50, CancellationToken.None);
            IReadOnlyList<Guid> others = await stories.ListOwnedIdsAsync(other.Id, 0, 50, CancellationToken.None);
            // Assert
            Assert.True(loaded?.IsOwnedBy(owner.Id));
            Assert.Equal([story.Id], owned);
            Assert.Empty(others);
        }
        finally
        {
            await stories.DeleteAsync(story.Id, CancellationToken.None);
            await artists.DeleteAsync(owner.Id, CancellationToken.None);
            await artists.DeleteAsync(other.Id, CancellationToken.None);
        }
    }

    [Fact]
    public async Task ListOwnedIdsAsync_InvalidPage_Throws()
    {
        // Act / Assert
        var stories = new StoryRepository(fixture.DataSource);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => stories.ListOwnedIdsAsync(TestOwner.Id, -1, 10, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => stories.ListOwnedIdsAsync(TestOwner.Id, 0, 51, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteArtist_OwnedStory_BecomesUnownedAndCanBeClaimed()
    {
        // Arrange
        var stories = new StoryRepository(fixture.DataSource);
        var artists = new ArtistRepository(fixture.DataSource);
        Artist leaving = await AddArtistAsync();
        Artist claimer = await AddArtistAsync();
        Story story = Story.Upload(Details, new StoryRecording("clip.wav", 12), true, leaving.Id);
        try
        {
            await stories.AddAsync(story, CancellationToken.None);
            // Act
            await artists.DeleteAsync(leaving.Id, CancellationToken.None);
            Assert.Null((await stories.GetAsync(story.Id, CancellationToken.None))?.OwnerId);
            int claimed = await stories.ClaimUnownedAsync(claimer.Id, CancellationToken.None);
            // Assert
            Assert.True(claimed >= 1);
            Assert.True((await stories.GetAsync(story.Id, CancellationToken.None))?.IsOwnedBy(claimer.Id));
            Assert.Equal(0, await stories.ClaimUnownedAsync(claimer.Id, CancellationToken.None));
        }
        finally
        {
            await stories.DeleteAsync(story.Id, CancellationToken.None);
            // The shared database may hold the owner's own unclaimed stories; deleting the claimer returns them to unowned.
            await artists.DeleteAsync(claimer.Id, CancellationToken.None);
        }
    }
}
