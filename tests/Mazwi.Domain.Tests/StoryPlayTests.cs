namespace Mazwi.Domain.Tests;

public sealed class StoryPlayTests
{
    private static readonly DateOnly Day = new(2026, 11, 11);

    private static Story ApprovedStory()
    {
        Story story = Story.Upload(new StoryDetails("Story", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 12), true, TestOwner.Id);
        story.SetTranscript("Kare kare");
        story.SetTranslation("xh", "Kudala");
        story.Approve("sn", story.ReviewVersion);
        return story;
    }

    [Fact]
    public void RecordPlay_ApprovedCurrentVersion_CountsTheLanguageShown()
    {
        // Arrange
        Story story = ApprovedStory();
        // Act
        StoryPlay? original = story.RecordPlay("sn", story.ReviewVersion, Day);
        StoryPlay? fallback = story.RecordPlay("xh", story.ReviewVersion, Day);
        story.Approve("xh", story.ReviewVersion);
        StoryPlay? translated = story.RecordPlay("xh", story.ReviewVersion, Day);
        // Assert
        Assert.Equal(new StoryPlay(story.Id, "sn", Day), original);
        Assert.Equal("sn", fallback!.Language);
        Assert.Equal("xh", translated!.Language);
    }

    [Fact]
    public void RecordPlay_UnapprovedStaleOrUnsupported_ReturnsNull()
    {
        // Arrange
        Story story = ApprovedStory();
        Story draft = Story.Upload(story.Details, story.Recording, true, TestOwner.Id);
        // Act / Assert
        Assert.Null(draft.RecordPlay("sn", draft.ReviewVersion, Day));
        Assert.Null(story.RecordPlay("sn", Guid.NewGuid(), Day));
        Assert.Null(story.RecordPlay("fr", story.ReviewVersion, Day));
        Assert.Null(story.RecordPlay(null!, story.ReviewVersion, Day));
    }

    [Fact]
    public void StoryPlay_MissingParts_Throws()
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => new StoryPlay(Guid.Empty, "sn", Day));
        Assert.Throws<DomainException>(() => new StoryPlay(Guid.NewGuid(), "fr", Day));
        Assert.Throws<DomainException>(() => new StoryPlay(Guid.NewGuid(), "sn", default));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(2, 3)]
    public void StoryPlayTotal_InvalidCounts_Throws(int plays, int recent)
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => new StoryPlayTotal(Guid.NewGuid(), "sn", plays, recent));
    }

    [Fact]
    public void StoryPlayTotal_ZeroAndEqualCounts_AreAllowed()
    {
        // Act / Assert
        Assert.Equal(0, new StoryPlayTotal(Guid.NewGuid(), "sn", 0, 0).Plays);
        Assert.Equal(5, new StoryPlayTotal(Guid.NewGuid(), "xh", 5, 5).PlaysLast7Days);
        Assert.Throws<DomainException>(() => new StoryPlayTotal(Guid.Empty, "sn", 1, 1));
    }
}
