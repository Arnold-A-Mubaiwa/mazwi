namespace Mazwi.Domain.Tests;

public sealed class ModerationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    private static Story Published()
    {
        Story story = Story.Upload(new StoryDetails("Story", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 12), true, TestOwner.Id);
        story.SetTranscript("Kare kare");
        story.Approve("sn", story.ReviewVersion);
        return story;
    }

    [Fact]
    public void HideForReview_PublishedStory_LeavesTheFeedAndCannotBePlayedOrReported()
    {
        // Arrange
        Story story = Published();
        // Act
        story.HideForReview();
        // Assert
        Assert.True(story.HiddenForReview);
        Assert.True(story.OriginalApproved);
        Assert.False(story.CanBeReported);
        Assert.Null(story.SelectApprovedVersion("sn"));
        Assert.Null(story.RecordPlay("sn", story.ReviewVersion, DateOnly.FromDateTime(Now.UtcDateTime)));
    }

    [Fact]
    public void RestoreAfterReview_HiddenStory_ReturnsToTheFeedWithoutChangingTheVersion()
    {
        // Arrange
        Story story = Published();
        Guid version = story.ReviewVersion;
        story.HideForReview();
        // Act
        story.RestoreAfterReview();
        // Assert
        Assert.NotNull(story.SelectApprovedVersion("sn"));
        Assert.Equal(version, story.ReviewVersion);
        Assert.True(Story.Rehydrate(story.Id, story.Details, story.Recording, story.PermissionRecordedAt, hiddenForReview: true).HiddenForReview);
    }

    [Fact]
    public void File_PublishedStory_CreatesReportWithTrimmedNote()
    {
        // Act
        StoryReport report = StoryReport.File(Published(), ReportReason.Misleading, "  The translation is wrong.  ", Now);
        // Assert
        Assert.Equal(ReportReason.Misleading, report.Reason);
        Assert.Equal("The translation is wrong.", report.Note);
        Assert.Equal(Now, report.CreatedAt);
        Assert.Null(StoryReport.File(Published(), ReportReason.Other, "   ", Now).Note);
    }

    [Fact]
    public void File_UnpublishedOrHiddenStory_Throws()
    {
        // Arrange
        Story hidden = Published();
        hidden.HideForReview();
        Story draft = Story.Upload(hidden.Details, hidden.Recording, true, TestOwner.Id);
        // Act / Assert
        Assert.Throws<DomainException>(() => StoryReport.File(hidden, ReportReason.Harmful, null, Now));
        Assert.Throws<DomainException>(() => StoryReport.File(draft, ReportReason.Harmful, null, Now));
        Assert.Throws<ArgumentNullException>(() => StoryReport.File(null!, ReportReason.Harmful, null, Now));
    }

    [Theory]
    [InlineData(501)]
    public void File_NoteTooLong_Throws(int length)
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => StoryReport.File(Published(), ReportReason.Other, new string('n', length), Now));
        Assert.Equal(500, StoryReport.File(Published(), ReportReason.Other, new string('n', 500), Now).Note!.Length);
    }

    [Fact]
    public void File_UnknownReasonOrControlCharacters_Throws()
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => StoryReport.File(Published(), (ReportReason)42, null, Now));
        Assert.Throws<DomainException>(() => StoryReport.File(Published(), ReportReason.Other, "bad\u0007note", Now));
        Assert.Throws<DomainException>(() => StoryReport.File(Published(), ReportReason.Other, null, default));
    }
}
