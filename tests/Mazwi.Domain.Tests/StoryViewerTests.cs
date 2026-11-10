namespace Mazwi.Domain.Tests;

public sealed class StoryViewerTests
{
    private static Story StoryWithText()
    {
        Story story = Story.Upload(new StoryDetails("Story", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 19), true);
        story.SetTranscript("Original"); story.SetTranslation("xh", "Translated text"); return story;
    }

    [Fact]
    public void SelectApprovedVersion_DraftOriginal_ReturnsNothing()
    {
        // Arrange
        Story story = StoryWithText();
        // Act / Assert
        Assert.Null(story.SelectApprovedVersion("sn")); Assert.Null(story.SelectApprovedVersion("xh"));
    }

    [Fact]
    public void SelectApprovedVersion_UnapprovedTranslation_ExposesOnlyOriginal()
    {
        // Arrange
        Story story = StoryWithText(); story.Approve("sn", story.ReviewVersion);
        // Act
        ApprovedStoryVersion version = story.SelectApprovedVersion("xh")!;
        // Assert
        Assert.Equal("Original", version.Text.Value); Assert.Equal("sn", version.TextLanguage); Assert.Equal("sn", version.AudioLanguage);
    }

    [Fact]
    public void SelectApprovedVersion_ApprovedTextWithoutRecording_LabelsOriginalAudio()
    {
        // Arrange
        Story story = StoryWithText(); story.Approve("sn", story.ReviewVersion); story.Approve("xh", story.ReviewVersion);
        // Act
        ApprovedStoryVersion version = story.SelectApprovedVersion("xh")!;
        // Assert
        Assert.Equal("xh", version.TextLanguage); Assert.Equal("sn", version.AudioLanguage); Assert.Equal(story.Id, version.MediaId);
    }

    [Fact]
    public void SelectApprovedVersion_ApprovedRecording_PrefersItAndAllowsOriginal()
    {
        // Arrange
        Story story = StoryWithText(); var media = new TranslatedRecording(Guid.NewGuid(), new StoryRecording("voice.mp3", 19));
        story.AttachRecording("xh", media, true); story.Approve("sn", story.ReviewVersion); story.Approve("xh", story.ReviewVersion);
        // Act / Assert
        Assert.Equal(media.Id, story.SelectApprovedVersion("xh")!.MediaId);
        Assert.Equal("xh", story.SelectApprovedVersion("xh")!.AudioLanguage);
        Assert.Equal(story.Id, story.SelectApprovedVersion("xh", true)!.MediaId);
        story.SetTranscript("Changed"); Assert.Null(story.SelectApprovedVersion("xh"));
        Assert.Throws<DomainException>(() => story.SelectApprovedVersion("xx"));
    }
}
