using Mazwi.Domain;

namespace Mazwi.Repository.Tests;

public sealed class SubtitleRepositoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly SubtitleTiming TwoCues = new([0, 1200], 3400);

    private async Task<Story> ApprovedStoryAsync(StoryRepository repository)
    {
        Story story = Story.Upload(new StoryDetails("Subtitle fixture", "Test artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 19), true, TestOwner.Id);
        await repository.AddAsync(story, CancellationToken.None);
        story.SetTranscript("Kare kare\nPane imwe nguva");
        await repository.SaveTranscriptAsync(story, CancellationToken.None);
        await repository.SaveTranslationAsync(story.Id, story.SetTranslation("xh", "Kudala\nKwakukho"), CancellationToken.None);
        story = (await repository.GetAsync(story.Id, CancellationToken.None))!;
        await repository.SaveApprovalAsync(story.Id, "sn", story.ReviewVersion, false, CancellationToken.None);
        await repository.SaveApprovalAsync(story.Id, "xh", story.ReviewVersion, false, CancellationToken.None);
        return (await repository.GetAsync(story.Id, CancellationToken.None))!;
    }

    private static async Task SaveTimingAsync(StoryRepository repository, Story story, string language)
    {
        Guid version = story.ReviewVersion;
        story.SetSubtitleTiming(language, TwoCues, version);
        await repository.SaveSubtitleTimingAsync(story, language, version, CancellationToken.None);
    }

    [Fact]
    public async Task SaveSubtitleTimingAsync_Translation_RoundTripsAndRevokesOnlyThatLanguage()
    {
        // Arrange
        var repository = new StoryRepository(fixture.DataSource);
        Story story = await ApprovedStoryAsync(repository);
        try
        {
            // Act
            await SaveTimingAsync(repository, story, "xh");
            Story loaded = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            // Assert
            Assert.Equal(TwoCues, loaded.SubtitleTimings["xh"]);
            Assert.Equal(story.ReviewVersion, loaded.ReviewVersion);
            Assert.True(loaded.OriginalApproved);
            Assert.False(loaded.Translations.Single().IsApproved);
        }
        finally { await repository.DeleteAsync(story.Id, CancellationToken.None); }
    }

    [Fact]
    public async Task SaveSubtitleTimingAsync_Original_RevokesEveryApprovalAndReplacesEarlierTiming()
    {
        // Arrange
        var repository = new StoryRepository(fixture.DataSource);
        Story story = await ApprovedStoryAsync(repository);
        try
        {
            // Act
            await SaveTimingAsync(repository, story, "sn");
            Guid version = story.ReviewVersion;
            var later = new SubtitleTiming([100, 2000], 5000);
            story.SetSubtitleTiming("sn", later, version);
            await repository.SaveSubtitleTimingAsync(story, "sn", version, CancellationToken.None);
            Story loaded = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            // Assert
            Assert.Equal(later, loaded.SubtitleTimings["sn"]);
            Assert.False(loaded.OriginalApproved);
            Assert.All(loaded.Translations, draft => Assert.False(draft.IsApproved));
        }
        finally { await repository.DeleteAsync(story.Id, CancellationToken.None); }
    }

    [Fact]
    public async Task SaveSubtitleTimingAsync_StaleVersion_ThrowsConflictAndSavesNothing()
    {
        // Arrange
        var repository = new StoryRepository(fixture.DataSource);
        Story story = await ApprovedStoryAsync(repository);
        try
        {
            Guid stale = story.ReviewVersion;
            Story concurrent = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            await SaveTimingAsync(repository, concurrent, "xh");
            story.SetSubtitleTiming("sn", TwoCues, stale);
            // Act / Assert
            await Assert.ThrowsAsync<StoryConflictException>(() => repository.SaveSubtitleTimingAsync(story, "sn", stale, CancellationToken.None));
            Story loaded = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            Assert.False(loaded.SubtitleTimings.ContainsKey("sn"));
            Assert.True(loaded.OriginalApproved);
        }
        finally { await repository.DeleteAsync(story.Id, CancellationToken.None); }
    }

    [Fact]
    public async Task TextAndRecordingSaves_RemoveOnlyTheAffectedTiming()
    {
        // Arrange
        var repository = new StoryRepository(fixture.DataSource);
        Story story = await ApprovedStoryAsync(repository);
        try
        {
            await SaveTimingAsync(repository, story, "sn");
            await SaveTimingAsync(repository, story, "xh");
            // Act
            await repository.SaveTranslationAsync(story.Id, story.SetTranslation("xh", "Kudala\nKwakukho", new TranslationNotes(["Note"])), CancellationToken.None);
            bool keptAfterNotes = (await repository.GetAsync(story.Id, CancellationToken.None))!.SubtitleTimings.ContainsKey("xh");
            story = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            Guid version = story.ReviewVersion;
            story.AttachRecording("xh", new TranslatedRecording(Guid.NewGuid(), new StoryRecording("xh.wav", 19)), true);
            await repository.SaveRecordingAsync(story.Id, story.Translations.Single(), version, CancellationToken.None);
            Story afterRecording = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            bool originalKept = afterRecording.SubtitleTimings.ContainsKey("sn");
            bool translationKept = afterRecording.SubtitleTimings.ContainsKey("xh");
            afterRecording.SetTranscript("Kare kare\nPane imwe nguva yakare");
            await repository.SaveTranscriptAsync(afterRecording, CancellationToken.None);
            Story afterTranscript = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            // Assert
            Assert.True(keptAfterNotes);
            Assert.False(translationKept);
            Assert.True(originalKept);
            Assert.Empty(afterTranscript.SubtitleTimings);
        }
        finally { await repository.DeleteAsync(story.Id, CancellationToken.None); }
    }

    [Fact]
    public async Task SaveTranslationAsync_ChangedText_RemovesThatTiming()
    {
        // Arrange
        var repository = new StoryRepository(fixture.DataSource);
        Story story = await ApprovedStoryAsync(repository);
        try
        {
            await SaveTimingAsync(repository, story, "xh");
            // Act
            await repository.SaveTranslationAsync(story.Id, story.SetTranslation("xh", "Kudala kakhulu\nKwakukho"), CancellationToken.None);
            // Assert
            Assert.Empty((await repository.GetAsync(story.Id, CancellationToken.None))!.SubtitleTimings);
        }
        finally { await repository.DeleteAsync(story.Id, CancellationToken.None); }
    }

    [Fact]
    public async Task SaveSubtitleTimingAsync_LanguageWithoutTiming_Throws()
    {
        // Arrange
        var repository = new StoryRepository(fixture.DataSource);
        Story story = Story.Upload(new StoryDetails("Subtitle fixture", "Test artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 19), true, TestOwner.Id);
        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveSubtitleTimingAsync(story, "sn", story.ReviewVersion, CancellationToken.None));
    }
}
