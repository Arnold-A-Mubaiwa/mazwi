namespace Mazwi.Domain.Tests;

public sealed class AiTranscriptTests
{
    private static Story ApprovedStory()
    {
        Story story = Story.Upload(new StoryDetails("Story", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 12), true);
        story.SetTranscript("Human transcript");
        story.SetTranslation("xh", "Molo");
        story.Approve("sn", story.ReviewVersion);
        story.Approve("xh", story.ReviewVersion);
        return story;
    }

    [Fact]
    public void SetAiTranscript_ApprovedStory_ReplacesTextAndRevokesEveryApproval()
    {
        // Arrange
        Story story = ApprovedStory();
        Guid version = story.ReviewVersion;
        // Act
        story.SetAiTranscript(new StoryText("AI transcript"), version);
        // Assert
        Assert.Equal("AI transcript", story.Transcript!.Value);
        Assert.False(story.OriginalApproved);
        Assert.False(story.Translations.Single().IsApproved);
        Assert.NotEqual(version, story.ReviewVersion);
    }

    [Fact]
    public void SetAiTranscript_NoTranscriptYet_SavesFirstDraft()
    {
        // Arrange
        Story story = Story.Upload(new StoryDetails("Story", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 12), true);
        // Act
        story.SetAiTranscript(new StoryText("First draft"), story.ReviewVersion);
        // Assert
        Assert.Equal("First draft", story.Transcript!.Value);
        Assert.False(story.OriginalApproved);
    }

    [Fact]
    public void SetAiTranscript_StaleVersion_ThrowsConflictAndKeepsText()
    {
        // Arrange
        Story story = ApprovedStory();
        // Act / Assert
        Assert.Throws<StoryConflictException>(() => story.SetAiTranscript(new StoryText("AI transcript"), Guid.NewGuid()));
        Assert.Equal("Human transcript", story.Transcript!.Value);
        Assert.True(story.OriginalApproved);
    }

    [Fact]
    public void SetAiTranscript_Null_ThrowsArgumentNullException()
    {
        // Arrange
        Story story = ApprovedStory();
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => story.SetAiTranscript(null!, story.ReviewVersion));
    }

    [Theory]
    [InlineData(StoryText.MaximumLength, true)]
    [InlineData(StoryText.MaximumLength + 1, false)]
    public void StoryText_TranscriptLengthBoundary_IsEnforced(int length, bool valid)
    {
        // Act
        Exception? error = Record.Exception(() => new StoryText(new string('a', length)));
        // Assert
        Assert.Equal(valid, error is null);
    }
}
