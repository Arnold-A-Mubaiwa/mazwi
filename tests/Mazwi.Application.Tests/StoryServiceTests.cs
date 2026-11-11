using Mazwi.Domain;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Mazwi.Application.Tests;

public sealed class StoryServiceTests
{
    private readonly IStoryRepository _repository = Substitute.For<IStoryRepository>();
    private readonly IStoryMediaStore _media = Substitute.For<IStoryMediaStore>();
    private static UploadStoryCommand Command(bool permission = true) => new("Story", "Artist", "Shona", "ZW", "folktale", "sn", permission, "clip.wav", 12, TestOwner.Id);

    [Fact]
    public async Task UploadAsync_ValidStory_SavesMediaBeforeMetadataAndPassesCancellation()
    {
        // Arrange
        var service = new StoryService(_repository, _media);
        using var stream = new MemoryStream(new byte[12]);
        using var cancellation = new CancellationTokenSource();
        // Act
        StoryDto story = await service.UploadAsync(Command(), stream, cancellation.Token);
        // Assert
        Assert.Equal("unpublished", story.Status);
        Received.InOrder(() =>
        {
            _media.SaveAsync(story.Id, Arg.Any<StoryRecording>(), stream, cancellation.Token);
            _repository.AddAsync(Arg.Is<Story>(s => s.Id == story.Id && s.OwnerId == TestOwner.Id), cancellation.Token);
        });
        await _media.DidNotReceiveWithAnyArgs().DeleteAsync(default, default!, default);
    }

    [Fact]
    public async Task UploadAsync_NoPermission_DoesNotWriteAnything()
    {
        // Arrange
        var service = new StoryService(_repository, _media);
        using var stream = new MemoryStream();
        // Act / Assert
        await Assert.ThrowsAsync<DomainException>(() => service.UploadAsync(Command(false), stream, CancellationToken.None));
        await _media.DidNotReceiveWithAnyArgs().SaveAsync(default, default!, default!, default);
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task UploadAsync_MediaFails_DoesNotPersistMetadata()
    {
        // Arrange
        _media.SaveAsync(default, default!, default!, default).ThrowsAsyncForAnyArgs(new IOException("storage failed"));
        var service = new StoryService(_repository, _media);
        using var stream = new MemoryStream();
        // Act / Assert
        await Assert.ThrowsAsync<IOException>(() => service.UploadAsync(Command(), stream, CancellationToken.None));
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UploadAsync_MetadataFailsOrIsCancelled_RemovesSavedMedia(bool cancelled)
    {
        // Arrange
        Exception failure = cancelled ? new OperationCanceledException() : new IOException("database failed");
        _repository.AddAsync(default!, default).ThrowsAsyncForAnyArgs(failure);
        var service = new StoryService(_repository, _media);
        using var stream = new MemoryStream();
        // Act / Assert
        Exception? actual = await Record.ExceptionAsync(() => service.UploadAsync(Command(), stream, CancellationToken.None));
        Assert.Same(failure, actual);
        await _media.Received(1).DeleteAsync(Arg.Any<Guid>(), Arg.Any<StoryRecording>(), CancellationToken.None);
    }

    [Fact]
    public async Task UploadAsync_AlreadyCancelled_DoesNotWriteAnything()
    {
        // Arrange
        var service = new StoryService(_repository, _media);
        using var stream = new MemoryStream();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        // Act / Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.UploadAsync(Command(), stream, cancellation.Token));
        await _media.DidNotReceiveWithAnyArgs().SaveAsync(default, default!, default!, default);
    }

    [Fact]
    public async Task GetAsync_ExistingStory_ReturnsMetadataWithoutStoragePath()
    {
        // Arrange
        Story story = Story.Upload(new StoryDetails("Story", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 12), true, TestOwner.Id);
        _repository.GetAsync(story.Id, Arg.Any<CancellationToken>()).Returns(story);
        var service = new StoryService(_repository, _media);
        // Act / Assert
        Assert.Equal(StoryDto.From(story), await service.GetAsync(story.Id, CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_MissingStory_ReturnsNull()
    {
        // Arrange
        var service = new StoryService(_repository, _media);
        // Act / Assert
        Assert.Null(await service.GetAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
