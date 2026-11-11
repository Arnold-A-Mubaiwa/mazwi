using Mazwi.Domain;

namespace Mazwi.Repository.Tests;

public sealed class LanguageCatalogueRepositoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task SaveAndLoad_NewCatalogueLanguages_RoundTripThroughEveryLanguageColumn()
    {
        // Arrange
        var repository = new StoryRepository(fixture.DataSource);
        var plays = new StoryPlayRepository(fixture.DataSource);
        Story story = Story.Upload(new StoryDetails("Ìjàpá àti Erin", "Test artist", "Yoruba", "NG", "folktale", "yo"), new StoryRecording("clip.wav", 19), true, TestOwner.Id);
        await repository.AddAsync(story, CancellationToken.None);
        try
        {
            // Act
            story.SetTranscript("Ní ayé àtijọ́"); await repository.SaveTranscriptAsync(story, CancellationToken.None);
            await repository.SaveTranslationAsync(story.Id, story.SetTranslation("am", "በጥንት ጊዜ"), CancellationToken.None);
            await repository.SaveTranslationAsync(story.Id, story.SetTranslation("zu", "Kudala"), CancellationToken.None);
            story = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            Guid timed = story.ReviewVersion;
            story.SetSubtitleTiming("am", new SubtitleTiming([0], 2000), timed);
            await repository.SaveSubtitleTimingAsync(story, "am", timed, CancellationToken.None);
            story = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            await repository.SaveApprovalAsync(story.Id, "yo", story.ReviewVersion, false, CancellationToken.None);
            await repository.SaveApprovalAsync(story.Id, "am", story.ReviewVersion, true, CancellationToken.None);
            await plays.RecordAsync(new StoryPlay(story.Id, "am", new DateOnly(2026, 11, 11)), CancellationToken.None);
            Story loaded = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            IReadOnlyList<StoryPlayTotal> totals = await plays.ListForOwnerAsync(TestOwner.Id, new DateOnly(2026, 11, 5), CancellationToken.None);
            // Assert
            Assert.Equal("yo", loaded.Details.Language);
            Assert.True(loaded.OriginalApproved);
            Assert.Equal(["am", "zu"], loaded.Translations.Select(draft => draft.Language).Order());
            Assert.True(loaded.Translations.Single(draft => draft.Language == "am").IsApproved);
            Assert.Contains(totals, total => total.StoryId == story.Id && total.Language == "am" && total.Plays == 1);
        }
        finally { await repository.DeleteAsync(story.Id, CancellationToken.None); }
    }
}
