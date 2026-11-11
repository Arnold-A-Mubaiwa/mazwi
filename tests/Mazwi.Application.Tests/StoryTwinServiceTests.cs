using Mazwi.Domain;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Mazwi.Application.Tests;

public sealed class StoryTwinServiceTests
{
    private sealed class FixedClock : TimeProvider
    {
        public static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private readonly IStoryTwinRepository _twins = Substitute.For<IStoryTwinRepository>();
    private readonly IStoryRepository _stories = Substitute.For<IStoryRepository>();
    private readonly IStoryFingerprinter _fingerprinter = Substitute.For<IStoryFingerprinter>();
    private static readonly StoryFingerprint Shona = new("Tsuro tricks Gudo at the river.", [1f, 0f]);
    private static readonly StoryFingerprint Unrelated = new("A girl finds a lost cow.", [0f, 1f]);

    private StoryTwinService Service() => new(_twins, _stories, _fingerprinter, new TwinThreshold(0.8), new FixedClock());

    [Fact]
    public async Task RefreshAsync_NewStory_SavesFingerprintAndSuggestsCloseStoriesByOtherArtists()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var work = new StoryToFingerprint(Guid.NewGuid(), "sn", "folktale", "Kare kare Tsuro naGudo");
        var twin = new TwinCandidate(Guid.NewGuid(), Guid.NewGuid(), "yo", new StoryFingerprint("Tsuro and Gudo at the river.", [0.95f, 0.05f]));
        var other = new TwinCandidate(Guid.NewGuid(), Guid.NewGuid(), "ha", Unrelated);
        _twins.ListStoriesToFingerprintAsync(FixedClock.Now - StoryTwinService.RetryAfterFailure, 10, cancellation.Token).Returns(new[] { work });
        _fingerprinter.FingerprintAsync(work.Text, "sn", "folktale", cancellation.Token).Returns(Shona);
        _twins.ListCandidatesAsync(cancellation.Token).Returns(new[] { new TwinCandidate(work.StoryId, Guid.NewGuid(), "sn", Shona), twin, other });
        // Act
        int refreshed = await Service().RefreshAsync(10, cancellation.Token);
        // Assert
        Assert.Equal(1, refreshed);
        await _twins.Received(1).SaveFingerprintAsync(work.StoryId, work.SourceHash, Shona, cancellation.Token);
        await _twins.Received(1).AddSuggestionsAsync(Arg.Is<IEnumerable<StoryTwin>>(pairs => pairs.Single().TwinOf(work.StoryId) == twin.StoryId),
            cancellation.Token);
    }

    [Fact]
    public async Task RefreshAsync_FingerprintUnavailable_RecordsFailureAndSuggestsNothing()
    {
        // Arrange
        var work = new StoryToFingerprint(Guid.NewGuid(), "sn", "story", "Kare kare");
        _twins.ListStoriesToFingerprintAsync(Arg.Any<DateTimeOffset>(), 10, Arg.Any<CancellationToken>()).Returns(new[] { work });
        _fingerprinter.FingerprintAsync(work.Text, "sn", "story", Arg.Any<CancellationToken>()).ThrowsAsync(new StoryFingerprintUnavailableException());
        // Act
        int refreshed = await Service().RefreshAsync(10, CancellationToken.None);
        // Assert
        Assert.Equal(0, refreshed);
        await _twins.Received(1).SaveFingerprintFailureAsync(work.StoryId, work.SourceHash, FixedClock.Now, Arg.Any<CancellationToken>());
        await _twins.DidNotReceiveWithAnyArgs().SaveFingerprintAsync(default, default!, default!, default);
        await _twins.DidNotReceiveWithAnyArgs().ListCandidatesAsync(default);
    }

    [Fact]
    public async Task RefreshAsync_NothingToDo_DoesNotCallAi()
    {
        // Arrange
        _twins.ListStoriesToFingerprintAsync(Arg.Any<DateTimeOffset>(), 5, Arg.Any<CancellationToken>()).Returns(Array.Empty<StoryToFingerprint>());
        // Act
        int refreshed = await Service().RefreshAsync(5, CancellationToken.None);
        // Assert
        Assert.Equal(0, refreshed);
        await _fingerprinter.DidNotReceiveWithAnyArgs().FingerprintAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task RefreshAsync_UnexpectedFailure_Propagates()
    {
        // Arrange
        var work = new StoryToFingerprint(Guid.NewGuid(), "sn", "story", "Kare kare");
        _twins.ListStoriesToFingerprintAsync(Arg.Any<DateTimeOffset>(), 10, Arg.Any<CancellationToken>()).Returns(new[] { work });
        _fingerprinter.FingerprintAsync(work.Text, "sn", "story", Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException());
        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().RefreshAsync(10, CancellationToken.None));
    }

    [Fact]
    public async Task ListForOwnerAsync_Views_MapsWithoutPrivateData()
    {
        // Arrange
        Guid owner = Guid.NewGuid();
        var view = new TwinView(Guid.NewGuid(), "Tsuro na Gudo", Guid.NewGuid(), "Ìjàpá", "ABS", "yo", "Yoruba, Nigeria", TwinStatus.Suggested);
        _twins.ListForOwnerAsync(owner, Arg.Any<CancellationToken>()).Returns(new[] { view });
        // Act
        StoryTwinDto dto = Assert.Single(await Service().ListForOwnerAsync(owner, CancellationToken.None));
        // Assert
        Assert.Equal(new StoryTwinDto(view.StoryId, "Tsuro na Gudo", view.TwinStoryId, "Ìjàpá", "ABS", "yo", "Yoruba, Nigeria", "suggested"), dto);
    }

    [Fact]
    public async Task ConfirmAndReject_VisiblePair_SavesNewStatus()
    {
        // Arrange
        Guid mine = Guid.NewGuid(), theirs = Guid.NewGuid();
        StoryTwin twin = StoryTwin.Suggest(mine, theirs, 0.9);
        _twins.GetVisibleAsync(mine, theirs, Arg.Any<CancellationToken>()).Returns(twin);
        // Act
        bool confirmed = await Service().ConfirmAsync(mine, theirs, CancellationToken.None);
        // Assert
        Assert.True(confirmed);
        await _twins.Received(1).SaveStatusAsync(Arg.Is<StoryTwin>(saved => saved.Status == TwinStatus.Confirmed), Arg.Any<CancellationToken>());
        Assert.True(await Service().RejectAsync(mine, theirs, CancellationToken.None));
        Assert.Equal(TwinStatus.Rejected, twin.Status);
    }

    [Fact]
    public async Task ConfirmAndReject_MissingOrIneligiblePair_ReturnsFalseWithoutSaving()
    {
        // Arrange
        _twins.GetVisibleAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((StoryTwin?)null);
        // Act / Assert
        Assert.False(await Service().ConfirmAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));
        Assert.False(await Service().RejectAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));
        await _twins.DidNotReceiveWithAnyArgs().SaveStatusAsync(default!, default);
    }

    [Fact]
    public async Task SetTwinsAllowedAsync_ExistingStory_SavesChoice()
    {
        // Arrange
        Story story = Story.Upload(new StoryDetails("Story", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 12), true, TestOwner.Id);
        _stories.GetAsync(story.Id, Arg.Any<CancellationToken>()).Returns(story);
        // Act
        StoryDraftDto? result = await Service().SetTwinsAllowedAsync(story.Id, false, CancellationToken.None);
        // Assert
        Assert.False(result!.TwinsAllowed);
        await _stories.Received(1).SaveTwinsAllowedAsync(Arg.Is<Story>(saved => !saved.TwinsAllowed), Arg.Any<CancellationToken>());
        _stories.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Story?)null);
        Assert.Null(await Service().SetTwinsAllowedAsync(Guid.NewGuid(), true, CancellationToken.None));
    }
}
