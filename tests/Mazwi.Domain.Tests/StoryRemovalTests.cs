namespace Mazwi.Domain.Tests;

public sealed class StoryRemovalTests
{
    private static Story PublishedStory(bool translatedRecording)
    {
        Story story = Story.Upload(new StoryDetails("Story", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 12), true, TestOwner.Id);
        story.SetTranscript("Kare kare");
        story.SetTranslation("xh", "Kudala");
        story.SetTranslation("en", "Long ago");
        if (translatedRecording)
        {
            story.AttachRecording("xh", new TranslatedRecording(Guid.NewGuid(), new StoryRecording("xh.mp3", 9)), true);
        }

        story.Approve("sn", story.ReviewVersion);
        story.Approve("xh", story.ReviewVersion);
        return story;
    }

    [Fact]
    public void Unpublish_PublishedStory_RemovesEveryApprovalAndChangesVersion()
    {
        // Arrange
        Story story = PublishedStory(false);
        Guid version = story.ReviewVersion;
        // Act
        story.Unpublish(version);
        // Assert
        Assert.False(story.OriginalApproved);
        Assert.Equal("unpublished", story.Status);
        Assert.All(story.Translations, draft => Assert.False(draft.IsApproved));
        Assert.NotEqual(version, story.ReviewVersion);
        Assert.Null(story.SelectApprovedVersion("sn"));
    }

    [Fact]
    public void Unpublish_AlreadyUnpublishedOrStale_Throws()
    {
        // Arrange
        Story story = PublishedStory(false);
        // Act / Assert
        Assert.Throws<StoryConflictException>(() => story.Unpublish(Guid.NewGuid()));
        story.Unpublish(story.ReviewVersion);
        Assert.Throws<DomainException>(() => story.Unpublish(story.ReviewVersion));
    }

    [Fact]
    public void Unpublish_KeepsTextRecordingsAndNotes()
    {
        // Arrange
        Story story = PublishedStory(true);
        // Act
        story.Unpublish(story.ReviewVersion);
        // Assert
        Assert.Equal("Kare kare", story.Transcript!.Value);
        Assert.NotNull(story.Translations.Single(draft => draft.Language == "xh").Recording);
    }

    [Fact]
    public void RecordingFiles_OriginalAndTranslatedRecordings_AreAllListed()
    {
        // Arrange
        Story story = PublishedStory(true);
        TranslatedRecording translated = story.Translations.Single(draft => draft.Language == "xh").Recording!;
        // Act
        IReadOnlyList<StoredRecording> files = story.RecordingFiles();
        // Assert
        Assert.Equal([new StoredRecording(story.Id, story.Recording), new StoredRecording(translated.Id, translated.File)], files);
        Assert.Single(PublishedStory(false).RecordingFiles());
    }
}
