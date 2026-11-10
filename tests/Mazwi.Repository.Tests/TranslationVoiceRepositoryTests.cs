using Mazwi.Domain;

namespace Mazwi.Repository.Tests;

public sealed class TranslationVoiceRepositoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task SaveRecordingAsync_GeneratedThenUploaded_RoundTripsVoiceAndRevokesOnlyTargetApproval()
    {
        // Arrange
        var repository = new StoryRepository(fixture.DataSource);
        Story story = Story.Upload(new StoryDetails("Voice test", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 12), true);
        await repository.AddAsync(story, CancellationToken.None);
        try
        {
            story.SetTranscript("Original");
            await repository.SaveTranscriptAsync(story, CancellationToken.None);
            await repository.SaveTranslationAsync(story.Id, story.SetTranslation("xh", "Molo"), CancellationToken.None);
            await repository.SaveTranslationAsync(story.Id, story.SetTranslation("en", "Hello"), CancellationToken.None);
            Story loaded = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            foreach (string language in new[] { "sn", "xh", "en" })
            {
                await repository.SaveApprovalAsync(story.Id, language, loaded.ReviewVersion, CancellationToken.None);
            }

            loaded = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            Guid version = loaded.ReviewVersion;
            loaded.AttachGeneratedVoice("xh", new TranslatedRecording(Guid.NewGuid(), new StoryRecording("voice.wav", 100), TranslatedRecording.StandardVoice), version);
            // Act
            await repository.SaveRecordingAsync(story.Id, loaded.Translations.Single(d => d.Language == "xh"), version, CancellationToken.None);
            Story generated = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            // Assert
            TranslationDraft generatedDraft = generated.Translations.Single(d => d.Language == "xh");
            Assert.False(generatedDraft.IsApproved);
            Assert.Equal(TranslatedRecording.StandardVoice, generatedDraft.Recording!.Voice);
            Assert.True(generated.Translations.Single(d => d.Language == "en").IsApproved);
            Assert.True(generated.OriginalApproved);
            Guid uploadVersion = generated.ReviewVersion;
            generated.AttachRecording("xh", new TranslatedRecording(Guid.NewGuid(), new StoryRecording("artist.mp3", 50)), true);
            await repository.SaveRecordingAsync(story.Id, generated.Translations.Single(d => d.Language == "xh"), uploadVersion, CancellationToken.None);
            TranslatedRecording uploaded = (await repository.GetAsync(story.Id, CancellationToken.None))!.Translations.Single(d => d.Language == "xh").Recording!;
            Assert.Null(uploaded.Voice);
            Assert.Equal(".mp3", uploaded.File.Extension);
        }
        finally { await repository.DeleteAsync(story.Id, CancellationToken.None); }
    }

    [Fact]
    public async Task SaveRecordingAsync_GeneratedVoice_IsReadBackAsStandard()
    {
        // Arrange
        var repository = new StoryRepository(fixture.DataSource);
        Story story = Story.Upload(new StoryDetails("Voice read test", "Artist", "Shona", "ZW", "poem", "sn"), new StoryRecording("clip.wav", 12), true);
        await repository.AddAsync(story, CancellationToken.None);
        try
        {
            story.SetTranscript("Original");
            await repository.SaveTranscriptAsync(story, CancellationToken.None);
            await repository.SaveTranslationAsync(story.Id, story.SetTranslation("xh", "Molo"), CancellationToken.None);
            Story loaded = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            Guid version = loaded.ReviewVersion;
            loaded.AttachGeneratedVoice("xh", new TranslatedRecording(Guid.NewGuid(), new StoryRecording("voice.wav", 100), TranslatedRecording.StandardVoice), version);
            // Act
            await repository.SaveRecordingAsync(story.Id, loaded.Translations.Single(d => d.Language == "xh"), version, CancellationToken.None);
            Story saved = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            // Assert
            TranslatedRecording recording = saved.Translations.Single(d => d.Language == "xh").Recording!;
            Assert.Equal(TranslatedRecording.StandardVoice, recording.Voice);
            Assert.Equal(".wav", recording.File.Extension);
            Assert.NotEqual(version, saved.ReviewVersion);
        }
        finally { await repository.DeleteAsync(story.Id, CancellationToken.None); }
    }

    [Fact]
    public async Task SaveRecordingAsync_StaleVersion_ThrowsConflictAndKeepsExistingRecording()
    {
        // Arrange
        var repository = new StoryRepository(fixture.DataSource);
        Story story = Story.Upload(new StoryDetails("Voice race test", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 12), true);
        await repository.AddAsync(story, CancellationToken.None);
        try
        {
            story.SetTranscript("Original");
            await repository.SaveTranscriptAsync(story, CancellationToken.None);
            await repository.SaveTranslationAsync(story.Id, story.SetTranslation("xh", "Molo"), CancellationToken.None);
            Story loaded = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            Guid staleVersion = loaded.ReviewVersion;
            loaded.AttachGeneratedVoice("xh", new TranslatedRecording(Guid.NewGuid(), new StoryRecording("voice.wav", 100), TranslatedRecording.StandardVoice), staleVersion);
            await repository.SaveTranslationAsync(story.Id, (await repository.GetAsync(story.Id, CancellationToken.None))!.SetTranslation("xh", "Molo, newer"), CancellationToken.None);
            // Act / Assert
            await Assert.ThrowsAsync<StoryConflictException>(() =>
                repository.SaveRecordingAsync(story.Id, loaded.Translations.Single(d => d.Language == "xh"), staleVersion, CancellationToken.None));
            TranslationDraft saved = (await repository.GetAsync(story.Id, CancellationToken.None))!.Translations.Single(d => d.Language == "xh");
            Assert.Null(saved.Recording);
            Assert.Equal("Molo, newer", saved.Text.Value);
        }
        finally { await repository.DeleteAsync(story.Id, CancellationToken.None); }
    }
}
