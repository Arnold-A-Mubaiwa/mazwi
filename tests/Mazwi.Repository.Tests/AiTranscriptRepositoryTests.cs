using Mazwi.Domain;

namespace Mazwi.Repository.Tests;

public sealed class AiTranscriptRepositoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task SaveAiTranscriptAsync_CurrentVersion_SavesTextAndRevokesEveryApproval()
    {
        // Arrange
        var repository = new StoryRepository(fixture.DataSource);
        Story story = Story.Upload(new StoryDetails("AI transcript test", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 12), true, TestOwner.Id);
        await repository.AddAsync(story, CancellationToken.None);
        try
        {
            story.SetTranscript("Human transcript");
            await repository.SaveTranscriptAsync(story, CancellationToken.None);
            await repository.SaveTranslationAsync(story.Id, story.SetTranslation("xh", "Molo"), CancellationToken.None);
            Story loaded = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            await repository.SaveApprovalAsync(story.Id, "sn", loaded.ReviewVersion, false, CancellationToken.None);
            await repository.SaveApprovalAsync(story.Id, "xh", loaded.ReviewVersion, false, CancellationToken.None);
            loaded = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            Guid version = loaded.ReviewVersion;
            loaded.SetAiTranscript(new StoryText("AI transcript"), version);
            // Act
            await repository.SaveAiTranscriptAsync(loaded, version, CancellationToken.None);
            Story saved = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            // Assert
            Assert.Equal("AI transcript", saved.Transcript!.Value);
            Assert.False(saved.OriginalApproved);
            Assert.False(saved.Translations.Single().IsApproved);
            Assert.NotEqual(version, saved.ReviewVersion);
        }
        finally { await repository.DeleteAsync(story.Id, CancellationToken.None); }
    }

    [Fact]
    public async Task SaveAiTranscriptAsync_StaleVersion_ThrowsConflictAndKeepsNewerText()
    {
        // Arrange
        var repository = new StoryRepository(fixture.DataSource);
        Story story = Story.Upload(new StoryDetails("AI transcript race", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 12), true, TestOwner.Id);
        await repository.AddAsync(story, CancellationToken.None);
        try
        {
            Story loaded = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            Guid staleVersion = loaded.ReviewVersion;
            loaded.SetAiTranscript(new StoryText("AI transcript"), staleVersion);
            Story newer = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            newer.SetTranscript("Artist typed this meanwhile");
            await repository.SaveTranscriptAsync(newer, CancellationToken.None);
            // Act / Assert
            await Assert.ThrowsAsync<StoryConflictException>(() => repository.SaveAiTranscriptAsync(loaded, staleVersion, CancellationToken.None));
            Assert.Equal("Artist typed this meanwhile", (await repository.GetAsync(story.Id, CancellationToken.None))!.Transcript!.Value);
        }
        finally { await repository.DeleteAsync(story.Id, CancellationToken.None); }
    }
}
