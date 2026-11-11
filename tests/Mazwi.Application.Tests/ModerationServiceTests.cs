using Mazwi.Domain;
using NSubstitute;

namespace Mazwi.Application.Tests;

public sealed class ModerationServiceTests
{
    private sealed class FixedClock : TimeProvider
    {
        public static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private readonly IStoryRepository _stories = Substitute.For<IStoryRepository>();
    private readonly IStoryReportRepository _reports = Substitute.For<IStoryReportRepository>();
    private readonly Story _story = Story.Upload(new StoryDetails("Story", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 12), true, TestOwner.Id);

    public ModerationServiceTests()
    {
        _story.SetTranscript("Kare kare");
        _stories.GetAsync(_story.Id, Arg.Any<CancellationToken>()).Returns(_story);
    }

    [Fact]
    public async Task ReportAsync_PublishedStory_SavesReport()
    {
        // Arrange
        _story.Approve("sn", _story.ReviewVersion);
        // Act
        bool reported = await new StoryReportService(_stories, _reports, new FixedClock()).ReportAsync(_story.Id, ReportReason.Harmful, "Hateful words", CancellationToken.None);
        // Assert
        Assert.True(reported);
        await _reports.Received(1).AddAsync(Arg.Is<StoryReport>(report => report.StoryId == _story.Id && report.Reason == ReportReason.Harmful
            && report.Note == "Hateful words" && report.CreatedAt == FixedClock.Now), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReportAsync_UnpublishedHiddenOrMissing_ReturnsFalseWithoutSaving()
    {
        // Arrange
        var service = new StoryReportService(_stories, _reports, new FixedClock());
        // Act / Assert
        Assert.False(await service.ReportAsync(_story.Id, ReportReason.Other, null, CancellationToken.None));
        _story.Approve("sn", _story.ReviewVersion);
        _story.HideForReview();
        Assert.False(await service.ReportAsync(_story.Id, ReportReason.Other, null, CancellationToken.None));
        Assert.False(await service.ReportAsync(Guid.NewGuid(), ReportReason.Other, null, CancellationToken.None));
        await _reports.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task HideAsync_Story_SavesHiddenAndKeepsReportsOpen()
    {
        // Act
        bool hidden = await new ModerationService(_stories, _reports, new FixedClock()).HideAsync(_story.Id, CancellationToken.None);
        // Assert
        Assert.True(hidden);
        await _stories.Received(1).SaveHiddenForReviewAsync(Arg.Is<Story>(story => story.HiddenForReview), Arg.Any<CancellationToken>());
        await _reports.DidNotReceiveWithAnyArgs().ResolveAsync(default, default, default);
    }

    [Fact]
    public async Task RestoreAsync_HiddenStory_UnhidesAndResolvesReports()
    {
        // Arrange
        _story.HideForReview();
        // Act
        bool restored = await new ModerationService(_stories, _reports, new FixedClock()).RestoreAsync(_story.Id, CancellationToken.None);
        // Assert
        Assert.True(restored);
        Assert.False(_story.HiddenForReview);
        await _stories.Received(1).SaveHiddenForReviewAsync(_story, Arg.Any<CancellationToken>());
        await _reports.Received(1).ResolveAsync(_story.Id, FixedClock.Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_Story_ClosesReportsWithoutChangingTheStory()
    {
        // Act
        bool resolved = await new ModerationService(_stories, _reports, new FixedClock()).ResolveAsync(_story.Id, CancellationToken.None);
        // Assert
        Assert.True(resolved);
        await _reports.Received(1).ResolveAsync(_story.Id, FixedClock.Now, Arg.Any<CancellationToken>());
        await _stories.DidNotReceiveWithAnyArgs().SaveHiddenForReviewAsync(default!, default);
    }

    [Fact]
    public async Task Actions_MissingStory_ReturnFalse()
    {
        // Arrange
        var service = new ModerationService(_stories, _reports, new FixedClock());
        Guid missing = Guid.NewGuid();
        // Act / Assert
        Assert.False(await service.HideAsync(missing, CancellationToken.None));
        Assert.False(await service.RestoreAsync(missing, CancellationToken.None));
        Assert.False(await service.ResolveAsync(missing, CancellationToken.None));
    }

    [Fact]
    public async Task ListAsync_OpenReports_MapsReasonsToNames()
    {
        // Arrange
        _reports.ListOpenAsync(100, Arg.Any<CancellationToken>()).Returns([new ReportedStory(_story.Id, "Story", "Artist", false, 2, [ReportReason.Harmful, ReportReason.Other], ["Note"], FixedClock.Now)]);
        // Act
        ReportedStoryDto item = Assert.Single(await new ModerationService(_stories, _reports, new FixedClock()).ListAsync(CancellationToken.None));
        // Assert
        Assert.Equal(["Harmful", "Other"], item.Reasons);
        Assert.Equal(2, item.OpenReports);
    }
}
