using Mazwi.Domain;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Mazwi.Application.Tests;

public sealed class StoryTranscriptServiceTests
{
    private readonly IStoryRepository _repository = Substitute.For<IStoryRepository>();
    private readonly IStoryMediaStore _mediaStore = Substitute.For<IStoryMediaStore>();
    private readonly ITranscriber _transcriber = Substitute.For<ITranscriber>();
    private readonly Story _story = Story.Upload(new StoryDetails("Story", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.m4a", 12), true);

    private StoryTranscriptService Service(bool recordingExists = true)
    {
        _story.SetTranscript("Human transcript");
        _story.Approve("sn", _story.ReviewVersion);
        _repository.GetAsync(_story.Id, Arg.Any<CancellationToken>()).Returns(_story);
        _mediaStore.OpenReadAsync(_story.Id, _story.Recording, Arg.Any<CancellationToken>()).Returns(_ => recordingExists ? new MemoryStream([1, 2]) : null);
        _transcriber.TranscribeAsync(Arg.Any<Stream>(), _story.Recording, Arg.Any<CancellationToken>()).Returns(new StoryText("AI transcript"));
        return new StoryTranscriptService(_repository, _mediaStore, _transcriber);
    }

    [Fact]
    public async Task DraftTranscriptAsync_StoredRecording_SavesUnapprovedTranscriptAndPassesCancellation()
    {
        // Arrange
        StoryTranscriptService service = Service();
        Guid version = _story.ReviewVersion;
        using var cancellation = new CancellationTokenSource();
        // Act
        StoryDraftDto? result = await service.DraftTranscriptAsync(_story.Id, version, cancellation.Token);
        // Assert
        Assert.Equal("AI transcript", result!.Transcript);
        Assert.False(result.OriginalApproved);
        await _transcriber.Received(1).TranscribeAsync(Arg.Any<Stream>(), _story.Recording, cancellation.Token);
        await _repository.Received(1).SaveAiTranscriptAsync(_story, version, cancellation.Token);
    }

    [Fact]
    public async Task DraftTranscriptAsync_MissingStory_ReturnsNullWithoutTranscribing()
    {
        // Arrange
        StoryTranscriptService service = Service();
        // Act
        StoryDraftDto? result = await service.DraftTranscriptAsync(Guid.NewGuid(), _story.ReviewVersion, CancellationToken.None);
        // Assert
        Assert.Null(result);
        await _transcriber.DidNotReceiveWithAnyArgs().TranscribeAsync(default!, default!, default);
    }

    [Fact]
    public async Task DraftTranscriptAsync_MissingRecordingFile_ThrowsNotAllowedWithoutTranscribing()
    {
        // Arrange
        StoryTranscriptService service = Service(recordingExists: false);
        // Act / Assert
        await Assert.ThrowsAsync<AiDraftNotAllowedException>(() => service.DraftTranscriptAsync(_story.Id, _story.ReviewVersion, CancellationToken.None));
        await _transcriber.DidNotReceiveWithAnyArgs().TranscribeAsync(default!, default!, default);
    }

    [Fact]
    public async Task DraftTranscriptAsync_StaleVersion_ThrowsConflictWithoutTranscribing()
    {
        // Arrange
        StoryTranscriptService service = Service();
        // Act / Assert
        await Assert.ThrowsAsync<StoryConflictException>(() => service.DraftTranscriptAsync(_story.Id, Guid.NewGuid(), CancellationToken.None));
        await _transcriber.DidNotReceiveWithAnyArgs().TranscribeAsync(default!, default!, default);
        await _mediaStore.DidNotReceiveWithAnyArgs().OpenReadAsync(default, default!, default);
    }

    [Fact]
    public async Task DraftTranscriptAsync_UpstreamFailure_SavesNothingAndKeepsTranscript()
    {
        // Arrange
        StoryTranscriptService service = Service();
        _transcriber.TranscribeAsync(default!, default!, default).ThrowsAsyncForAnyArgs(new TranscriptionUnavailableException());
        // Act / Assert
        await Assert.ThrowsAsync<TranscriptionUnavailableException>(() => service.DraftTranscriptAsync(_story.Id, _story.ReviewVersion, CancellationToken.None));
        await _repository.DidNotReceiveWithAnyArgs().SaveAiTranscriptAsync(default!, default, default);
        Assert.Equal("Human transcript", _story.Transcript!.Value);
        Assert.True(_story.OriginalApproved);
    }

    [Fact]
    public async Task DraftTranscriptAsync_Cancelled_PropagatesAndSavesNothing()
    {
        // Arrange
        StoryTranscriptService service = Service();
        _transcriber.TranscribeAsync(default!, default!, default).ThrowsAsyncForAnyArgs(new OperationCanceledException());
        // Act / Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.DraftTranscriptAsync(_story.Id, _story.ReviewVersion, CancellationToken.None));
        await _repository.DidNotReceiveWithAnyArgs().SaveAiTranscriptAsync(default!, default, default);
    }

    [Fact]
    public async Task DraftTranscriptAsync_SaveConflict_Propagates()
    {
        // Arrange
        StoryTranscriptService service = Service();
        _repository.SaveAiTranscriptAsync(default!, default, default).ThrowsAsyncForAnyArgs(new StoryConflictException());
        // Act / Assert
        await Assert.ThrowsAsync<StoryConflictException>(() => service.DraftTranscriptAsync(_story.Id, _story.ReviewVersion, CancellationToken.None));
    }
}
