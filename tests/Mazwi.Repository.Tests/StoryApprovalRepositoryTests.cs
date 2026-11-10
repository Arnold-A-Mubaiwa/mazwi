using Mazwi.Domain;

namespace Mazwi.Repository.Tests;

public sealed class StoryApprovalRepositoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task ApprovalAndEditing_RealPostgres_EnforcesVersionsAndInvalidation()
    {
        // Arrange
        var repository = new StoryRepository(fixture.DataSource);
        Story story = Story.Upload(new StoryDetails("Approval fixture", "Test artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 19), true);
        await repository.AddAsync(story, CancellationToken.None);
        try
        {
            story.SetTranscript("Original"); await repository.SaveTranscriptAsync(story, CancellationToken.None);
            await repository.SaveTranslationAsync(story.Id, story.SetTranslation("xh", "Translation"), CancellationToken.None);
            story = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            Guid version = story.ReviewVersion;
            // Act
            await repository.SaveApprovalAsync(story.Id, "sn", version, false, CancellationToken.None);
            await repository.SaveApprovalAsync(story.Id, "xh", version, false, CancellationToken.None);
            Story approved = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            // Assert
            Assert.True(approved.OriginalApproved); Assert.Equal("approved", Assert.Single(approved.Translations).Status);
            await repository.SaveTranscriptAsync(approved, CancellationToken.None);
            Assert.True((await repository.GetAsync(story.Id, CancellationToken.None))!.OriginalApproved);
            approved.SetTranslation("xh", "Changed");
            await repository.SaveTranslationAsync(story.Id, approved.Translations.Single(), CancellationToken.None);
            Story changed = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            Assert.True(changed.OriginalApproved); Assert.Equal("draft", Assert.Single(changed.Translations).Status);
            await Assert.ThrowsAsync<StoryConflictException>(() => repository.SaveApprovalAsync(story.Id, "xh", version, false, CancellationToken.None));
            var media = new TranslatedRecording(Guid.NewGuid(), new StoryRecording("voice.wav", 19));
            Guid current = changed.ReviewVersion;
            changed.AttachRecording("xh", media, true);
            await repository.SaveRecordingAsync(story.Id, changed.Translations.Single(), current, CancellationToken.None);
            changed = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            Assert.Equal(media, Assert.Single(changed.Translations).Recording);
            changed.SetTranscript("Changed original");
            await repository.SaveTranscriptAsync(changed, CancellationToken.None);
            Story unpublished = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            Assert.False(unpublished.OriginalApproved); Assert.Equal("unpublished", unpublished.Status);
            Assert.All(unpublished.Translations, d => Assert.False(d.IsApproved));
        }
        finally { await repository.DeleteAsync(story.Id, CancellationToken.None); }
        Assert.Null(await repository.GetAsync(story.Id, CancellationToken.None));
    }
}
