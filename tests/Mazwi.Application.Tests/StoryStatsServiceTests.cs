using Mazwi.Domain;
using NSubstitute;

namespace Mazwi.Application.Tests;

public sealed class StoryStatsServiceTests
{
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static readonly FixedClock Clock = new(new DateTimeOffset(2026, 11, 11, 23, 30, 0, TimeSpan.FromHours(-2)));
    private readonly IStoryRepository _stories = Substitute.For<IStoryRepository>();
    private readonly IStoryPlayRepository _plays = Substitute.For<IStoryPlayRepository>();
    private readonly Story _story = Story.Upload(new StoryDetails("Story", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 12), true, TestOwner.Id);

    public StoryStatsServiceTests()
    {
        _story.SetTranscript("Kare kare");
        _stories.GetAsync(_story.Id, Arg.Any<CancellationToken>()).Returns(_story);
    }

    [Fact]
    public async Task RecordPlayAsync_ApprovedVersion_RecordsOnTheUtcDay()
    {
        // Arrange
        _story.Approve("sn", _story.ReviewVersion);
        using var cancellation = new CancellationTokenSource();
        // Act
        bool counted = await new StoryStatsService(_stories, _plays, Clock).RecordPlayAsync(_story.Id, "sn", _story.ReviewVersion, cancellation.Token);
        // Assert
        Assert.True(counted);
        await _plays.Received(1).RecordAsync(new StoryPlay(_story.Id, "sn", new DateOnly(2026, 11, 12)), cancellation.Token);
    }

    [Fact]
    public async Task RecordPlayAsync_UnapprovedStaleOrMissing_DoesNotRecord()
    {
        // Arrange
        var service = new StoryStatsService(_stories, _plays, Clock);
        // Act / Assert
        Assert.False(await service.RecordPlayAsync(_story.Id, "sn", _story.ReviewVersion, CancellationToken.None));
        _story.Approve("sn", _story.ReviewVersion);
        Assert.False(await service.RecordPlayAsync(_story.Id, "sn", Guid.NewGuid(), CancellationToken.None));
        Assert.False(await service.RecordPlayAsync(Guid.NewGuid(), "sn", _story.ReviewVersion, CancellationToken.None));
        await _plays.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
    }

    [Fact]
    public async Task GetForOwnerAsync_Totals_GroupsByStoryWithSevenDayWindow()
    {
        // Arrange
        Guid other = Guid.NewGuid();
        _plays.ListForOwnerAsync(TestOwner.Id, new DateOnly(2026, 11, 6), Arg.Any<CancellationToken>()).Returns(
        [
            new StoryPlayTotal(_story.Id, "sn", 3, 1),
            new StoryPlayTotal(_story.Id, "xh", 7, 7),
            new StoryPlayTotal(other, "en", 2, 0),
        ]);
        // Act
        IReadOnlyList<StoryStatsDto> stats = await new StoryStatsService(_stories, _plays, Clock).GetForOwnerAsync(TestOwner.Id, CancellationToken.None);
        // Assert
        StoryStatsDto story = stats.Single(item => item.StoryId == _story.Id);
        Assert.Equal(10, story.Plays);
        Assert.Equal(8, story.PlaysLast7Days);
        Assert.Equal(["xh", "sn"], story.Languages.Select(language => language.Language));
        Assert.Equal(2, stats.Single(item => item.StoryId == other).Plays);
    }

    [Fact]
    public async Task GetForOwnerAsync_NoPlays_ReturnsEmpty()
    {
        // Act / Assert
        Assert.Empty(await new StoryStatsService(_stories, _plays, Clock).GetForOwnerAsync(TestOwner.Id, CancellationToken.None));
    }
}
