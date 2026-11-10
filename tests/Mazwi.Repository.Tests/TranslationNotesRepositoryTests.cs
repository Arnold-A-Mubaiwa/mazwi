using Mazwi.Domain;

namespace Mazwi.Repository.Tests;

public sealed class TranslationNotesRepositoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task SaveTranslationAsync_NotesRoundTripAndDismissal_RevokesOnlyTargetApproval()
    {
        // Arrange
        var repository = new StoryRepository(fixture.DataSource);
        Story story = Story.Upload(new StoryDetails("Notes test", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 12), true);
        await repository.AddAsync(story, CancellationToken.None);
        try
        {
            story.SetTranscript("Original");
            await repository.SaveTranscriptAsync(story, CancellationToken.None);
            await repository.SaveTranslationAsync(story.Id, story.SetTranslation("xh", "Translation", new TranslationNotes(["Sekuru's name; -- text", "Second note"])), CancellationToken.None);
            await repository.SaveTranslationAsync(story.Id, story.SetTranslation("en", "English"), CancellationToken.None);
            Story loaded = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            Assert.Empty(loaded.Translations.Single(d => d.Language == "en").Notes.Items);
            foreach (string language in new[] { "sn", "xh", "en" })
            {
                await repository.SaveApprovalAsync(story.Id, language, loaded.ReviewVersion, CancellationToken.None);
            }

            loaded = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            // Act
            await repository.SaveTranslationAsync(story.Id, loaded.SetTranslation("xh", "Translation", new TranslationNotes()), CancellationToken.None);
            Story saved = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            // Assert
            Assert.Empty(saved.Translations.Single(d => d.Language == "xh").Notes.Items);
            Assert.False(saved.Translations.Single(d => d.Language == "xh").IsApproved);
            Assert.True(saved.Translations.Single(d => d.Language == "en").IsApproved);
            Assert.True(saved.OriginalApproved);
        }
        finally { await repository.DeleteAsync(story.Id, CancellationToken.None); }
    }

    [Fact]
    public async Task SaveAiTranslationAsync_StaleVersion_DoesNotOverwriteNewerContent()
    {
        // Arrange
        var repository = new StoryRepository(fixture.DataSource);
        Story story = Story.Upload(new StoryDetails("AI race test", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 12), true);
        await repository.AddAsync(story, CancellationToken.None);
        try
        {
            story.SetTranscript("Original"); await repository.SaveTranscriptAsync(story, CancellationToken.None);
            Story loaded = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            await repository.SaveApprovalAsync(story.Id, "sn", loaded.ReviewVersion, CancellationToken.None);
            loaded = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            Guid version = loaded.ReviewVersion;
            var result = new TranslationDraftResult(new StoryText("AI text"), new TranslationNotes(["AI note"]));
            TranslationDraft ai = loaded.SetAiTranslation("xh", result, version);
            await repository.SaveTranslationAsync(story.Id, new TranslationDraft("xh", "Newer human text"), CancellationToken.None);
            // Act / Assert
            await Assert.ThrowsAsync<StoryConflictException>(() => repository.SaveAiTranslationAsync(story.Id, ai, version, CancellationToken.None));
            Story latest = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            Assert.Equal("Newer human text", Assert.Single(latest.Translations).Text.Value);
            // Act: a fresh AI draft saves notes and revokes even an identical approved translation.
            await repository.SaveApprovalAsync(story.Id, "xh", latest.ReviewVersion, CancellationToken.None);
            latest = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            Guid freshVersion = latest.ReviewVersion;
            await repository.SaveAiTranslationAsync(story.Id, latest.SetAiTranslation("xh", result, freshVersion), freshVersion, CancellationToken.None);
            Story saved = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            Assert.Equal("AI note", Assert.Single(Assert.Single(saved.Translations).Notes.Items));
            Assert.False(Assert.Single(saved.Translations).IsApproved);
            await repository.SaveApprovalAsync(story.Id, "xh", saved.ReviewVersion, CancellationToken.None);
            await repository.SaveAiTranslationAsync(story.Id, new TranslationDraft("xh", "AI text", notes: new TranslationNotes(["AI note"])), saved.ReviewVersion, CancellationToken.None);
            Assert.False(Assert.Single((await repository.GetAsync(story.Id, CancellationToken.None))!.Translations).IsApproved);
        }
        finally { await repository.DeleteAsync(story.Id, CancellationToken.None); }
    }
}
