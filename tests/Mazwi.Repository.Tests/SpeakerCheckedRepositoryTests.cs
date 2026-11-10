using Mazwi.Domain;

namespace Mazwi.Repository.Tests;

public sealed class SpeakerCheckedRepositoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task SaveApprovalAsync_SpeakerCheck_SavesReloadsUpdatesAndResetsOnEdit()
    {
        // Arrange
        var repository = new StoryRepository(fixture.DataSource);
        Story story = Story.Upload(new StoryDetails("Speaker check test", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 12), true);
        await repository.AddAsync(story, CancellationToken.None);
        try
        {
            story.SetTranscript("Kare kare"); await repository.SaveTranscriptAsync(story, CancellationToken.None);
            await repository.SaveTranslationAsync(story.Id, story.SetTranslation("xh", "Kudala"), CancellationToken.None);
            Guid version = (await repository.GetAsync(story.Id, CancellationToken.None))!.ReviewVersion;
            await repository.SaveApprovalAsync(story.Id, "sn", version, false, CancellationToken.None);
            // Act
            await repository.SaveApprovalAsync(story.Id, "xh", version, false, CancellationToken.None);
            Story notChecked = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            await repository.SaveApprovalAsync(story.Id, "xh", version, true, CancellationToken.None);
            Story isChecked = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            // Assert
            Assert.True(notChecked.Translations.Single().IsApproved); Assert.False(notChecked.Translations.Single().SpeakerChecked);
            Assert.True(isChecked.Translations.Single().SpeakerChecked);
            Assert.True(isChecked.SelectApprovedVersion("sn")!.SpeakerChecked);
            isChecked.SetTranslation("xh", "Kudala kakhulu");
            await repository.SaveTranslationAsync(story.Id, isChecked.Translations.Single(), CancellationToken.None);
            Story edited = (await repository.GetAsync(story.Id, CancellationToken.None))!;
            Assert.False(edited.Translations.Single().IsApproved); Assert.False(edited.Translations.Single().SpeakerChecked);
        }
        finally { await repository.DeleteAsync(story.Id, CancellationToken.None); }
    }
}
