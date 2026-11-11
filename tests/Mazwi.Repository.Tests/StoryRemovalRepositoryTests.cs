using Dapper;
using Mazwi.Domain;
using Npgsql;

namespace Mazwi.Repository.Tests;

public sealed class StoryRemovalRepositoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private async Task<Story> PublishedStoryAsync(StoryRepository repository)
    {
        Story story = Story.Upload(new StoryDetails("Removal fixture", "Test artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 19), true, TestOwner.Id);
        await repository.AddAsync(story, CancellationToken.None);
        story.SetTranscript("Kare kare");
        await repository.SaveTranscriptAsync(story, CancellationToken.None);
        await repository.SaveTranslationAsync(story.Id, story.SetTranslation("xh", "Kudala"), CancellationToken.None);
        story = (await repository.GetAsync(story.Id, CancellationToken.None))!;
        await repository.SaveApprovalAsync(story.Id, "sn", story.ReviewVersion, false, CancellationToken.None);
        await repository.SaveApprovalAsync(story.Id, "xh", story.ReviewVersion, false, CancellationToken.None);
        return (await repository.GetAsync(story.Id, CancellationToken.None))!;
    }

    [Fact]
    public async Task SaveUnpublishAsync_PublishedStory_RemovesApprovalsAndLeavesTheFeed()
    {
        // Arrange
        var repository = new StoryRepository(fixture.DataSource);
        Story story = await PublishedStoryAsync(repository);
        try
        {
            Guid version = story.ReviewVersion;
            story.Unpublish(version);
            // Act
            await repository.SaveUnpublishAsync(story, version, CancellationToken.None);
            Story loaded = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            // Assert
            Assert.False(loaded.OriginalApproved);
            Assert.All(loaded.Translations, draft => Assert.False(draft.IsApproved));
            Assert.Equal(story.ReviewVersion, loaded.ReviewVersion);
            Assert.DoesNotContain(story.Id, await repository.ListIdsAsync(true, 0, 50, CancellationToken.None));
            await Assert.ThrowsAsync<StoryConflictException>(() => repository.SaveUnpublishAsync(story, version, CancellationToken.None));
        }
        finally { await repository.DeleteAsync(story.Id, CancellationToken.None); }
    }

    [Fact]
    public async Task DeleteAsync_CurrentVersion_RemovesTheStoryAndEveryChildRow()
    {
        // Arrange
        var repository = new StoryRepository(fixture.DataSource);
        Story story = await PublishedStoryAsync(repository);
        Guid version = story.ReviewVersion;
        story.SetSubtitleTiming("sn", new SubtitleTiming([0], 900), version);
        await repository.SaveSubtitleTimingAsync(story, "sn", version, CancellationToken.None);
        await new StoryPlayRepository(fixture.DataSource).RecordAsync(new StoryPlay(story.Id, "sn", new DateOnly(2026, 11, 11)), CancellationToken.None);
        // Act
        bool deleted = await repository.DeleteAsync(story.Id, story.ReviewVersion, CancellationToken.None);
        // Assert
        Assert.True(deleted);
        Assert.Null(await repository.GetAsync(story.Id, CancellationToken.None));
        await using NpgsqlConnection connection = await fixture.DataSource.OpenConnectionAsync();
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("""
            SELECT (SELECT count(*) FROM story_translation_drafts WHERE story_id = @Id) + (SELECT count(*) FROM story_approvals WHERE story_id = @Id)
                + (SELECT count(*) FROM story_subtitle_timings WHERE story_id = @Id) + (SELECT count(*) FROM story_play_counts WHERE story_id = @Id)
            """, new { story.Id }));
    }

    [Fact]
    public async Task DeleteAsync_StaleVersion_ThrowsAndKeepsTheStory()
    {
        // Arrange
        var repository = new StoryRepository(fixture.DataSource);
        Story story = await PublishedStoryAsync(repository);
        try
        {
            // Act / Assert
            await Assert.ThrowsAsync<StoryConflictException>(() => repository.DeleteAsync(story.Id, Guid.NewGuid(), CancellationToken.None));
            Assert.NotNull(await repository.GetAsync(story.Id, CancellationToken.None));
        }
        finally { await repository.DeleteAsync(story.Id, CancellationToken.None); }
    }

    [Fact]
    public async Task DeleteAsync_MissingStory_ReturnsFalse()
    {
        // Act / Assert
        Assert.False(await new StoryRepository(fixture.DataSource).DeleteAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));
    }
}
