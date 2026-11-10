using Mazwi.Domain;
using Npgsql;

namespace Mazwi.Repository.Tests;

public sealed class StoryRepositoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task AddAndGetAsync_ConsentedStory_RoundTripsAllMetadata()
    {
        // Arrange
        var repository = new StoryRepository(fixture.DataSource);
        Story story = Story.Upload(new StoryDetails("Tsuro's tale; DROP TABLE stories;--", "Rudo", "Shona", "ZW", "folktale", "sn"), new StoryRecording("clip.mp4", 200), true);
        // Act
        await repository.AddAsync(story, CancellationToken.None);
        Story? loaded = await repository.GetAsync(story.Id, CancellationToken.None);
        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(story.Details, loaded.Details);
        Assert.Equal(story.Recording, loaded.Recording);
        Assert.Equal("unpublished", loaded.Status);
        Assert.InRange((story.PermissionRecordedAt - loaded.PermissionRecordedAt).Duration(), TimeSpan.Zero, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task GetAsync_MissingStory_ReturnsNull()
    {
        // Arrange
        var repository = new StoryRepository(fixture.DataSource);
        // Act / Assert
        Assert.Null(await repository.GetAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task AddAsync_DuplicateIdentity_RejectsDuplicate()
    {
        // Arrange
        var repository = new StoryRepository(fixture.DataSource);
        Story story = Story.Upload(new StoryDetails("Story", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 12), true);
        await repository.AddAsync(story, CancellationToken.None);
        // Act / Assert
        PostgresException error = await Assert.ThrowsAsync<PostgresException>(() => repository.AddAsync(story, CancellationToken.None));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
    }

    [Fact]
    public async Task GetAsync_Cancelled_StopsDatabaseWork()
    {
        // Arrange
        var repository = new StoryRepository(fixture.DataSource);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        // Act / Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.GetAsync(Guid.NewGuid(), cancellation.Token));
    }
}
