using Mazwi.Domain;

namespace Mazwi.Repository.Tests;

public sealed class StoryPlayRepositoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateOnly Today = new(2026, 11, 11);

    private async Task<(Story Story, Artist Owner)> StoryAsync()
    {
        Artist owner = Artist.Rehydrate(Guid.NewGuid(), new EmailAddress($"plays-{Guid.NewGuid():N}@mazwi.test"), new PasswordHash("hash"), DateTimeOffset.UtcNow);
        await new ArtistRepository(fixture.DataSource).AddAsync(owner, CancellationToken.None);
        Story story = Story.Upload(new StoryDetails("Play fixture", "Test artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 19), true, owner.Id);
        await new StoryRepository(fixture.DataSource).AddAsync(story, CancellationToken.None);
        return (story, owner);
    }

    private async Task CleanUpAsync(Story story, Artist owner)
    {
        await new StoryRepository(fixture.DataSource).DeleteAsync(story.Id, CancellationToken.None);
        await new ArtistRepository(fixture.DataSource).DeleteAsync(owner.Id, CancellationToken.None);
    }

    [Fact]
    public async Task RecordAsync_RepeatedPlays_CountsPerLanguageAndDay()
    {
        // Arrange
        var plays = new StoryPlayRepository(fixture.DataSource);
        (Story story, Artist owner) = await StoryAsync();
        try
        {
            // Act
            await plays.RecordAsync(new StoryPlay(story.Id, "sn", Today), CancellationToken.None);
            await plays.RecordAsync(new StoryPlay(story.Id, "sn", Today), CancellationToken.None);
            await plays.RecordAsync(new StoryPlay(story.Id, "sn", Today.AddDays(-10)), CancellationToken.None);
            await plays.RecordAsync(new StoryPlay(story.Id, "xh", Today.AddDays(-6)), CancellationToken.None);
            IReadOnlyList<StoryPlayTotal> totals = await plays.ListForOwnerAsync(owner.Id, Today.AddDays(-6), CancellationToken.None);
            // Assert
            StoryPlayTotal shona = totals.Single(total => total.Language == "sn");
            Assert.Equal(3, shona.Plays);
            Assert.Equal(2, shona.PlaysLast7Days);
            StoryPlayTotal xhosa = totals.Single(total => total.Language == "xh");
            Assert.Equal(1, xhosa.PlaysLast7Days);
        }
        finally { await CleanUpAsync(story, owner); }
    }

    [Fact]
    public async Task ListForOwnerAsync_OtherArtistsStories_AreNotIncluded()
    {
        // Arrange
        var plays = new StoryPlayRepository(fixture.DataSource);
        (Story story, Artist owner) = await StoryAsync();
        (Story otherStory, Artist other) = await StoryAsync();
        try
        {
            await plays.RecordAsync(new StoryPlay(otherStory.Id, "sn", Today), CancellationToken.None);
            // Act
            IReadOnlyList<StoryPlayTotal> mine = await plays.ListForOwnerAsync(owner.Id, Today, CancellationToken.None);
            // Assert
            Assert.Empty(mine);
            Assert.Single(await plays.ListForOwnerAsync(other.Id, Today, CancellationToken.None));
        }
        finally
        {
            await CleanUpAsync(story, owner);
            await CleanUpAsync(otherStory, other);
        }
    }

    [Fact]
    public async Task DeleteStory_RecordedPlays_AreRemovedWithIt()
    {
        // Arrange
        var plays = new StoryPlayRepository(fixture.DataSource);
        (Story story, Artist owner) = await StoryAsync();
        await plays.RecordAsync(new StoryPlay(story.Id, "sn", Today), CancellationToken.None);
        // Act
        await CleanUpAsync(story, owner);
        // Assert
        Assert.Empty(await plays.ListForOwnerAsync(owner.Id, Today, CancellationToken.None));
        await Assert.ThrowsAnyAsync<Exception>(() => plays.RecordAsync(new StoryPlay(story.Id, "sn", Today), CancellationToken.None));
    }

    [Fact]
    public async Task RecordAsync_NullPlay_Throws()
    {
        // Act / Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => new StoryPlayRepository(fixture.DataSource).RecordAsync(null!, CancellationToken.None));
    }
}
