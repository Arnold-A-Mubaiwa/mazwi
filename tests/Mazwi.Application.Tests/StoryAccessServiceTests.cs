using Mazwi.Domain;
using NSubstitute;

namespace Mazwi.Application.Tests;

public sealed class StoryAccessServiceTests
{
    private readonly IStoryRepository _repository = Substitute.For<IStoryRepository>();
    private static readonly StoryDetails Details = new("Story", "Artist", "Shona", "ZW", "story", "sn");

    [Fact]
    public async Task CheckOwnerAsync_Owner_ReturnsAllowed()
    {
        // Arrange
        Story story = Story.Upload(Details, new StoryRecording("clip.wav", 12), true, TestOwner.Id);
        _repository.GetAsync(story.Id, Arg.Any<CancellationToken>()).Returns(story);
        // Act / Assert
        Assert.Equal(StoryAccess.Allowed, await new StoryAccessService(_repository).CheckOwnerAsync(story.Id, TestOwner.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CheckOwnerAsync_OtherArtistOrLegacyStory_ReturnsDenied()
    {
        // Arrange
        Story owned = Story.Upload(Details, new StoryRecording("clip.wav", 12), true, TestOwner.Id);
        Story legacy = Story.Rehydrate(Guid.NewGuid(), Details, new StoryRecording("clip.wav", 12), DateTimeOffset.UtcNow);
        _repository.GetAsync(owned.Id, Arg.Any<CancellationToken>()).Returns(owned);
        _repository.GetAsync(legacy.Id, Arg.Any<CancellationToken>()).Returns(legacy);
        var service = new StoryAccessService(_repository);
        // Act / Assert
        Assert.Equal(StoryAccess.Denied, await service.CheckOwnerAsync(owned.Id, Guid.NewGuid(), CancellationToken.None));
        Assert.Equal(StoryAccess.Denied, await service.CheckOwnerAsync(legacy.Id, TestOwner.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CheckOwnerAsync_MissingStory_ReturnsNotFoundAndPassesCancellation()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        Guid id = Guid.NewGuid();
        // Act
        StoryAccess access = await new StoryAccessService(_repository).CheckOwnerAsync(id, TestOwner.Id, cancellation.Token);
        // Assert
        Assert.Equal(StoryAccess.NotFound, access);
        await _repository.Received(1).GetAsync(id, cancellation.Token);
    }
}
