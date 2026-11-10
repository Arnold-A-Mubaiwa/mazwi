using Mazwi.Domain;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Mazwi.Application.Tests;

public sealed class StoryReviewServiceTests
{
    private readonly IStoryRepository _repository = Substitute.For<IStoryRepository>();
    private readonly IStoryMediaStore _media = Substitute.For<IStoryMediaStore>();
    private readonly Story _story = Story.Upload(new StoryDetails("Story", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 19), true);
    private StoryReviewService Service()
    {
        _story.SetTranscript("Original"); _story.SetTranslation("xh", "Translation");
        _repository.GetAsync(_story.Id, Arg.Any<CancellationToken>()).Returns(_story);
        return new StoryReviewService(_repository, _media);
    }

    [Fact]
    public async Task ApproveAsync_CurrentVersionAndAvailableRecording_SavesApproval()
    {
        // Arrange
        StoryReviewService service = Service(); Guid version = _story.ReviewVersion;
        _media.OpenReadAsync(_story.Id, _story.Recording, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Stream?>(new MemoryStream(new byte[19])));
        // Act
        StoryDraftDto? result = await service.ApproveAsync(_story.Id, "sn", version, false, CancellationToken.None);
        // Assert
        Assert.NotNull(result); Assert.True(result.OriginalApproved);
        await _repository.Received(1).SaveApprovalAsync(_story.Id, "sn", version, false, CancellationToken.None);
    }

    [Fact]
    public async Task ApproveAsync_StaleOrMissingMedia_DoesNotPersistApproval()
    {
        // Arrange
        StoryReviewService service = Service();
        // Act / Assert
        await Assert.ThrowsAsync<StoryConflictException>(() => service.ApproveAsync(_story.Id, "sn", Guid.NewGuid(), false, CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => service.ApproveAsync(_story.Id, "sn", _story.ReviewVersion, false, CancellationToken.None));
        await _repository.DidNotReceiveWithAnyArgs().SaveApprovalAsync(default, default!, default, default, default);
    }

    [Fact]
    public async Task UploadRecordingAsync_DatabaseFails_DeletesOnlyNewFile()
    {
        // Arrange
        StoryReviewService service = Service();
        var command = new UploadTranslationRecordingCommand(_story.Id, "xh", "voice.wav", 19, true, _story.ReviewVersion);
        _repository.SaveRecordingAsync(default, default!, default, default).ThrowsAsyncForAnyArgs(new IOException("failure"));
        using var input = new MemoryStream();
        // Act / Assert
        await Assert.ThrowsAsync<IOException>(() => service.UploadRecordingAsync(command, input, CancellationToken.None));
        await _media.Received(1).DeleteAsync(Arg.Is<Guid>(id => id != _story.Id), Arg.Any<StoryRecording>(), CancellationToken.None);
    }

    [Fact]
    public async Task UploadRecordingAsync_NoPermission_DoesNotSaveMedia()
    {
        // Arrange
        StoryReviewService service = Service(); using var input = new MemoryStream();
        // Act / Assert
        await Assert.ThrowsAsync<DomainException>(() => service.UploadRecordingAsync(new(_story.Id, "xh", "voice.wav", 19, false, _story.ReviewVersion), input, CancellationToken.None));
        await _media.DidNotReceiveWithAnyArgs().SaveAsync(default, default!, default!, default);
    }

    [Fact]
    public async Task GetRecordingAsync_TranslatedTrack_UsesItsGeneratedIdentity()
    {
        // Arrange
        StoryReviewService service = Service(); var file = new StoryRecording("voice.mp3", 19); Guid id = Guid.NewGuid();
        _story.AttachRecording("xh", new TranslatedRecording(id, file), true);
        using var stream = new MemoryStream(new byte[19]);
        _media.OpenReadAsync(id, file, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Stream?>(stream));
        // Act
        RecordingPlaybackDto? playback = await service.GetRecordingAsync(_story.Id, "xh", CancellationToken.None);
        // Assert
        Assert.NotNull(playback); Assert.Same(stream, playback.Content); Assert.Equal("audio/mpeg", playback.ContentType);
    }

    [Fact]
    public async Task AllMethods_MissingStory_ReturnNull()
    {
        // Arrange
        var service = new StoryReviewService(_repository, _media); using var input = new MemoryStream(); Guid id = Guid.NewGuid();
        // Act / Assert
        Assert.Null(await service.ApproveAsync(id, "sn", Guid.NewGuid(), false, CancellationToken.None));
        Assert.Null(await service.GetRecordingAsync(id, "sn", CancellationToken.None));
        Assert.Null(await service.UploadRecordingAsync(new(id, "xh", "file.wav", 19, true, Guid.NewGuid()), input, CancellationToken.None));
    }
}
