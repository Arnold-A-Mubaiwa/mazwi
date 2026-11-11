using Mazwi.Domain;
using NSubstitute;

namespace Mazwi.Application.Tests;

public sealed class StorySubtitleServiceTests
{
    private readonly IStoryRepository _repository = Substitute.For<IStoryRepository>();
    private readonly IStoryMediaStore _media = Substitute.For<IStoryMediaStore>();
    private readonly Story _story = Story.Upload(new StoryDetails("Story", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 12), true, TestOwner.Id);

    public StorySubtitleServiceTests()
    {
        _story.SetTranscript("Kare kare\nPane imwe nguva");
        _repository.GetAsync(_story.Id, Arg.Any<CancellationToken>()).Returns(_story);
    }

    [Fact]
    public async Task SaveTimingAsync_ValidTiming_SavesWithExpectedVersionAndReturnsIt()
    {
        // Arrange
        Guid version = _story.ReviewVersion;
        using var cancellation = new CancellationTokenSource();
        // Act
        StoryDraftDto? result = await new StorySubtitleService(_repository).SaveTimingAsync(_story.Id, "sn", [0, 1000], 3000, version, cancellation.Token);
        // Assert
        Assert.Equal([0, 1000], result!.OriginalTiming!.StartsMs);
        Assert.Equal(3000, result.OriginalTiming.EndMs);
        Assert.NotEqual(version, result.ReviewVersion);
        await _repository.Received(1).SaveSubtitleTimingAsync(_story, "sn", version, cancellation.Token);
    }

    [Fact]
    public async Task SaveTimingAsync_MissingStory_ReturnsNull()
    {
        // Act / Assert
        Assert.Null(await new StorySubtitleService(_repository).SaveTimingAsync(Guid.NewGuid(), "sn", [0], 10, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task SaveTimingAsync_InvalidOrMismatchedTiming_ThrowsWithoutSaving()
    {
        // Arrange
        var service = new StorySubtitleService(_repository);
        // Act / Assert
        await Assert.ThrowsAsync<DomainException>(() => service.SaveTimingAsync(_story.Id, "sn", [1000, 0], 3000, _story.ReviewVersion, CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => service.SaveTimingAsync(_story.Id, "sn", [0], 3000, _story.ReviewVersion, CancellationToken.None));
        await _repository.DidNotReceiveWithAnyArgs().SaveSubtitleTimingAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task SaveTimingAsync_StaleVersion_ThrowsConflictWithoutSaving()
    {
        // Act / Assert
        await Assert.ThrowsAsync<StoryConflictException>(() => new StorySubtitleService(_repository).SaveTimingAsync(_story.Id, "sn", [0, 1000], 3000, Guid.NewGuid(), CancellationToken.None));
        await _repository.DidNotReceiveWithAnyArgs().SaveSubtitleTimingAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task FeedGetSubtitlesAsync_ApprovedTimedStory_ReturnsCuesForPlayedVersion()
    {
        // Arrange
        _story.SetSubtitleTiming("sn", new SubtitleTiming([0, 1000], 3000), _story.ReviewVersion);
        _story.Approve("sn", _story.ReviewVersion);
        _media.OpenReadAsync(_story.Id, _story.Recording, Arg.Any<CancellationToken>()).Returns(_ => new MemoryStream([1]));
        var feed = new StoryFeedService(_repository, _media);
        // Act
        IReadOnlyList<SubtitleCueDto>? cues = await feed.GetSubtitlesAsync(_story.Id, "sn", _story.ReviewVersion, false, CancellationToken.None);
        StoryViewDto? view = await feed.GetAsync(_story.Id, "sn", CancellationToken.None);
        // Assert
        Assert.Equal([new SubtitleCueDto(0, 1000, "Kare kare"), new SubtitleCueDto(1000, 3000, "Pane imwe nguva")], cues);
        Assert.Equal(cues, view!.Cues);
        await Assert.ThrowsAsync<StoryConflictException>(() => feed.GetSubtitlesAsync(_story.Id, "sn", Guid.NewGuid(), false, CancellationToken.None));
    }

    [Fact]
    public async Task FeedGetSubtitlesAsync_UnapprovedOrMissingStory_ReturnsNull()
    {
        // Arrange
        var feed = new StoryFeedService(_repository, _media);
        // Act / Assert
        Assert.Null(await feed.GetSubtitlesAsync(_story.Id, "sn", _story.ReviewVersion, false, CancellationToken.None));
        Assert.Null(await feed.GetSubtitlesAsync(Guid.NewGuid(), "sn", _story.ReviewVersion, false, CancellationToken.None));
    }
}
