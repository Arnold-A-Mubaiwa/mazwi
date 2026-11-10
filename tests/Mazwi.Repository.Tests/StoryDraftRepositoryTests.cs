using Mazwi.Domain;

namespace Mazwi.Repository.Tests;

public sealed class StoryDraftRepositoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private async Task<Story> AddStoryAsync()
    {
        Story story = Story.Upload(new StoryDetails("Draft test", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 12), true);
        await new StoryRepository(fixture.DataSource).AddAsync(story, CancellationToken.None);
        return story;
    }

    [Fact]
    public async Task SaveTranscriptAndTranslationAsync_RealPostgres_PreservesTextAndOtherLanguages()
    {
        // Arrange
        Story story = await AddStoryAsync();
        var repository = new StoryRepository(fixture.DataSource);
        string original = "Tsuro's words\n\nSekuru Rudo; -- still story text";
        story.SetTranscript(original);
        // Act
        await repository.SaveTranscriptAsync(story, CancellationToken.None);
        await repository.SaveTranslationAsync(story.Id, story.SetTranslation("xh", "First translation"), CancellationToken.None);
        await repository.SaveTranslationAsync(story.Id, story.SetTranslation("en", "English\nDraft"), CancellationToken.None);
        await repository.SaveTranslationAsync(story.Id, story.SetTranslation("xh", "Updated isiXhosa draft"), CancellationToken.None);
        Story? loaded = await new StoryRepository(fixture.DataSource).GetAsync(story.Id, CancellationToken.None);
        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(original, loaded.Transcript?.Value);
        Assert.Equal(2, loaded.Translations.Count);
        Assert.Equal("Updated isiXhosa draft", loaded.Translations.Single(draft => draft.Language == "xh").Text.Value);
        Assert.Equal("English\nDraft", loaded.Translations.Single(draft => draft.Language == "en").Text.Value);
        Assert.Equal("unpublished", loaded.Status);
        loaded.SetTranscript("Revised original");
        await repository.SaveTranscriptAsync(loaded, CancellationToken.None);
        Story? revised = await repository.GetAsync(story.Id, CancellationToken.None);
        Assert.NotNull(revised);
        Assert.Equal("Revised original", revised.Transcript?.Value);
        Assert.Equal(2, revised.Translations.Count);
    }

    [Fact]
    public async Task GetAsync_UneditedUpload_ReturnsEmptyDrafts()
    {
        // Arrange
        Story story = await AddStoryAsync();
        // Act
        Story? loaded = await new StoryRepository(fixture.DataSource).GetAsync(story.Id, CancellationToken.None);
        // Assert
        Assert.NotNull(loaded);
        Assert.Null(loaded.Transcript);
        Assert.Empty(loaded.Translations);
    }

    [Fact]
    public async Task SaveTranscriptAsync_MissingStory_ReportsFailedUpdate()
    {
        // Arrange
        Story story = Story.Upload(new StoryDetails("Missing", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 12), true);
        story.SetTranscript("Original");
        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => new StoryRepository(fixture.DataSource).SaveTranscriptAsync(story, CancellationToken.None));
    }

    [Fact]
    public async Task SaveTranscriptAsync_NoText_RejectsWrite()
    {
        // Arrange
        Story story = await AddStoryAsync();
        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() => new StoryRepository(fixture.DataSource).SaveTranscriptAsync(story, CancellationToken.None));
    }

    [Fact]
    public async Task SaveTranslationAsync_Cancelled_StopsDatabaseWork()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        // Act / Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new StoryRepository(fixture.DataSource)
            .SaveTranslationAsync(Guid.NewGuid(), new TranslationDraft("xh", "Words"), cancellation.Token));
    }
}
