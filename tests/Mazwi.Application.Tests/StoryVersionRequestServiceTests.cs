using Mazwi.Domain;
using NSubstitute;

namespace Mazwi.Application.Tests;

public sealed class StoryVersionRequestServiceTests
{
    private readonly IStoryRepository _stories = Substitute.For<IStoryRepository>();
    private readonly IStoryVersionRequestRepository _requests = Substitute.For<IStoryVersionRequestRepository>();
    private readonly IStoryTwinRepository _twins = Substitute.For<IStoryTwinRepository>();

    private static Story Published(string language, string title = "Story")
    {
        Story story = Story.Upload(new StoryDetails(title, "Artist", "Community", "ZW", "folktale", language), new StoryRecording("clip.wav", 12), true, TestOwner.Id);
        story.SetTranscript("Words");
        story.Approve(language, story.ReviewVersion);
        return story;
    }

    [Fact]
    public async Task RequestAsync_PublishedStoryMissingLanguage_SavesAnonymousRequest()
    {
        // Arrange
        Story story = Published("yo");
        _stories.GetAsync(story.Id, Arg.Any<CancellationToken>()).Returns(story);
        // Act
        bool accepted = await new StoryVersionRequestService(_stories, _requests).RequestAsync(story.Id, "sn", CancellationToken.None);
        // Assert
        Assert.True(accepted);
        await _requests.Received(1).AddAsync(Arg.Is<VersionRequest>(request => request.StoryId == story.Id && request.Language == "sn"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestAsync_MissingUnpublishedOrApprovedLanguage_DoesNotSave()
    {
        // Arrange
        Story published = Published("yo");
        Story draft = Story.Upload(new StoryDetails("Draft", "Artist", "Community", "ZW", "story", "yo"), new StoryRecording("clip.wav", 12), true, TestOwner.Id);
        _stories.GetAsync(published.Id, Arg.Any<CancellationToken>()).Returns(published);
        _stories.GetAsync(draft.Id, Arg.Any<CancellationToken>()).Returns(draft);
        var service = new StoryVersionRequestService(_stories, _requests);
        // Act / Assert
        Assert.False(await service.RequestAsync(Guid.NewGuid(), "sn", CancellationToken.None));
        Assert.False(await service.RequestAsync(draft.Id, "sn", CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => service.RequestAsync(published.Id, "yo", CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => service.RequestAsync(published.Id, "de", CancellationToken.None));
        await _requests.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task ListForOwnerAsync_Totals_MapToDtos()
    {
        // Arrange
        Guid owner = Guid.NewGuid(), story = Guid.NewGuid();
        _requests.ListForOwnerAsync(owner, Arg.Any<CancellationToken>()).Returns(new[] { new VersionRequestTotal(story, "sn", 3) });
        // Act
        IReadOnlyList<VersionRequestDto> totals = await new StoryVersionRequestService(_stories, _requests).ListForOwnerAsync(owner, CancellationToken.None);
        // Assert
        Assert.Equal([new VersionRequestDto(story, "sn", 3)], totals);
    }

    [Fact]
    public async Task ListForViewerAsync_ConfirmedTwins_SayWhetherTheyAreInTheViewersLanguage()
    {
        // Arrange
        Story shona = Published("sn", "Tsuro na Gudo");
        Story yoruba = Published("yo", "Ìjàpá");
        Story hausa = Published("ha", "Kura");
        hausa.SetTranslation("sn", "Kare kare");
        hausa.Approve("sn", hausa.ReviewVersion);
        Story optedOut = Published("zu", "Unogwaja");
        optedOut.AllowTwins(false);
        _stories.GetAsync(shona.Id, Arg.Any<CancellationToken>()).Returns(shona);
        _stories.GetAsync(yoruba.Id, Arg.Any<CancellationToken>()).Returns(yoruba);
        _stories.GetAsync(hausa.Id, Arg.Any<CancellationToken>()).Returns(hausa);
        _stories.GetAsync(optedOut.Id, Arg.Any<CancellationToken>()).Returns(optedOut);
        _twins.ListConfirmedTwinIdsAsync(shona.Id, Arg.Any<CancellationToken>()).Returns(new[] { yoruba.Id, hausa.Id, optedOut.Id, Guid.NewGuid() });
        var service = new StoryTwinService(_twins, _stories, Substitute.For<IStoryFingerprinter>(), new TwinThreshold(0.8), TimeProvider.System);
        // Act
        IReadOnlyList<ViewerTwinDto>? twins = await service.ListForViewerAsync(shona.Id, "sn", CancellationToken.None);
        // Assert
        Assert.Equal([
            new ViewerTwinDto(yoruba.Id, "Ìjàpá", "Artist", "Community", "yo", false),
            new ViewerTwinDto(hausa.Id, "Kura", "Artist", "Community", "ha", true),
        ], twins);
        Assert.Null(await service.ListForViewerAsync(Guid.NewGuid(), "sn", CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => service.ListForViewerAsync(shona.Id, "de", CancellationToken.None));
    }
}
