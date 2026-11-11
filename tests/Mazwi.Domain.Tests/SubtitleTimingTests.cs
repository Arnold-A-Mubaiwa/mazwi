namespace Mazwi.Domain.Tests;

public sealed class SubtitleTimingTests
{
    private static readonly StoryText TwoLines = new("Kare kare\r\n\n  Pane imwe nguva  \n");

    [Fact]
    public void Constructor_IncreasingStarts_KeepsTimes()
    {
        // Act
        var timing = new SubtitleTiming([0, 1500], 4000);
        // Assert
        Assert.Equal([0, 1500], timing.StartsMs);
        Assert.Equal(4000, timing.EndMs);
    }

    [Theory]
    [InlineData(new int[0], 1000)]
    [InlineData(new[] { -1, 100 }, 1000)]
    [InlineData(new[] { 100, 100 }, 1000)]
    [InlineData(new[] { 500, 100 }, 1000)]
    [InlineData(new[] { 0, 900 }, 900)]
    [InlineData(new[] { 0 }, SubtitleTiming.MaximumEndMs + 1)]
    public void Constructor_InvalidTimes_Throws(int[] starts, int end)
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => new SubtitleTiming(starts, end));
    }

    [Fact]
    public void Constructor_BoundaryCueCounts_AcceptsMaximumAndRejectsMore()
    {
        // Act
        var maximum = new SubtitleTiming(Enumerable.Range(0, SubtitleTiming.MaximumCues), SubtitleTiming.MaximumCues);
        // Assert
        Assert.Equal(SubtitleTiming.MaximumCues, maximum.StartsMs.Count);
        Assert.Throws<DomainException>(() => new SubtitleTiming(Enumerable.Range(0, SubtitleTiming.MaximumCues + 1), SubtitleTiming.MaximumCues + 1));
        Assert.Throws<DomainException>(() => new SubtitleTiming(null!, 10));
    }

    [Fact]
    public void Lines_TextWithBlankLinesAndCarriageReturns_ReturnsTrimmedNonEmptyLines()
    {
        // Act / Assert
        Assert.Equal(["Kare kare", "Pane imwe nguva"], SubtitleTiming.Lines(TwoLines));
    }

    [Fact]
    public void Cues_MatchingText_EachCueEndsWhereTheNextStarts()
    {
        // Arrange
        var timing = new SubtitleTiming([200, 1500], 4000);
        // Act
        IReadOnlyList<SubtitleCue> cues = timing.Cues(TwoLines);
        // Assert
        Assert.True(timing.Fits(TwoLines));
        Assert.Equal([new SubtitleCue(200, 1500, "Kare kare"), new SubtitleCue(1500, 4000, "Pane imwe nguva")], cues);
    }

    [Fact]
    public void Cues_DifferentLineCount_ThrowsAndDoesNotFit()
    {
        // Arrange
        var timing = new SubtitleTiming([0], 1000);
        // Act / Assert
        Assert.False(timing.Fits(TwoLines));
        Assert.Throws<DomainException>(() => timing.Cues(TwoLines));
    }

    [Fact]
    public void Equals_SameTimes_AreEqual()
    {
        // Act / Assert
        Assert.Equal(new SubtitleTiming([0, 10], 20), new SubtitleTiming([0, 10], 20));
        Assert.NotEqual(new SubtitleTiming([0, 10], 20), new SubtitleTiming([0, 11], 20));
        Assert.Equal(new SubtitleTiming([0, 10], 20).GetHashCode(), new SubtitleTiming([0, 10], 20).GetHashCode());
    }
}
