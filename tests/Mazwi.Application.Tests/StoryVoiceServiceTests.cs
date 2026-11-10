using Mazwi.Domain;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Mazwi.Application.Tests;

public sealed class StoryVoiceServiceTests
{
    private readonly IStoryRepository _repository = Substitute.For<IStoryRepository>();
    private readonly IStoryMediaStore _mediaStore = Substitute.For<IStoryMediaStore>();
    private readonly ISpeechGenerator _generator = Substitute.For<ISpeechGenerator>();
    private readonly Story _story = Story.Upload(new StoryDetails("Folktale", "Artist", "Shona", "ZW", "folktale", "sn"), new StoryRecording("clip.wav", 12), true);

    private StoryVoiceService Service(string? translation = "Molo. Unjani?")
    {
        _story.SetTranscript("Original");
        if (translation is not null)
        {
            _story.SetTranslation("xh", translation);
        }

        _repository.GetAsync(_story.Id, Arg.Any<CancellationToken>()).Returns(_story);
        _generator.GenerateAsync(Arg.Any<SpeechScript>(), "xh", "folktale", Arg.Any<CancellationToken>())
            .Returns(new GeneratedSpeech(new byte[SpeechScript.WavHeaderBytes + 4]));
        return new StoryVoiceService(_repository, _mediaStore, _generator);
    }

    [Fact]
    public async Task GenerateVoiceAsync_SavedTranslation_StoresStandardVoiceAndPassesCancellation()
    {
        // Arrange
        StoryVoiceService service = Service();
        Guid version = _story.ReviewVersion;
        using var cancellation = new CancellationTokenSource();
        // Act
        StoryDraftDto? result = await service.GenerateVoiceAsync(_story.Id, "xh", version, cancellation.Token);
        // Assert
        TranslationDraftDto draft = Assert.Single(result!.Translations);
        Assert.Equal(TranslatedRecording.StandardVoice, draft.RecordingVoice);
        Assert.Equal(".wav", draft.RecordingExtension);
        Assert.Equal("draft", draft.Status);
        await _generator.Received(1).GenerateAsync(Arg.Is<SpeechScript>(s => s.Parts.Single() == "Molo. Unjani?"), "xh", "folktale", cancellation.Token);
        await _mediaStore.Received(1).SaveAsync(draft.RecordingId!.Value, Arg.Is<StoryRecording>(r => r.Bytes == SpeechScript.WavHeaderBytes + 4), Arg.Any<Stream>(), cancellation.Token);
        await _repository.Received(1).SaveRecordingAsync(_story.Id, Arg.Is<TranslationDraft>(d => d.Recording!.Voice == "standard"), version, cancellation.Token);
    }

    [Fact]
    public async Task GenerateVoiceAsync_MissingStory_ReturnsNullWithoutGenerating()
    {
        // Arrange
        StoryVoiceService service = Service();
        // Act
        StoryDraftDto? result = await service.GenerateVoiceAsync(Guid.NewGuid(), "xh", _story.ReviewVersion, CancellationToken.None);
        // Assert
        Assert.Null(result);
        await _generator.DidNotReceiveWithAnyArgs().GenerateAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task GenerateVoiceAsync_NoSavedTranslation_ThrowsNotAllowedWithoutGenerating()
    {
        // Arrange
        StoryVoiceService service = Service(translation: null);
        // Act / Assert
        await Assert.ThrowsAsync<AiDraftNotAllowedException>(() => service.GenerateVoiceAsync(_story.Id, "xh", _story.ReviewVersion, CancellationToken.None));
        await _generator.DidNotReceiveWithAnyArgs().GenerateAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task GenerateVoiceAsync_OversizedText_ThrowsNotAllowedWithoutGenerating()
    {
        // Arrange
        StoryVoiceService service = Service(new string('x', SpeechScript.MaximumCharacters + 1));
        // Act / Assert
        await Assert.ThrowsAsync<AiDraftNotAllowedException>(() => service.GenerateVoiceAsync(_story.Id, "xh", _story.ReviewVersion, CancellationToken.None));
        await _generator.DidNotReceiveWithAnyArgs().GenerateAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task GenerateVoiceAsync_StaleVersion_ThrowsConflictWithoutGenerating()
    {
        // Arrange
        StoryVoiceService service = Service();
        // Act / Assert
        await Assert.ThrowsAsync<StoryConflictException>(() => service.GenerateVoiceAsync(_story.Id, "xh", Guid.NewGuid(), CancellationToken.None));
        await _generator.DidNotReceiveWithAnyArgs().GenerateAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task GenerateVoiceAsync_UpstreamFailure_SavesNothing()
    {
        // Arrange
        StoryVoiceService service = Service();
        _generator.GenerateAsync(default!, default!, default!, default).ThrowsAsyncForAnyArgs(new SpeechUnavailableException());
        // Act / Assert
        await Assert.ThrowsAsync<SpeechUnavailableException>(() => service.GenerateVoiceAsync(_story.Id, "xh", _story.ReviewVersion, CancellationToken.None));
        await _mediaStore.DidNotReceiveWithAnyArgs().SaveAsync(default, default!, default!, default);
        await _repository.DidNotReceiveWithAnyArgs().SaveRecordingAsync(default, default!, default, default);
    }

    [Fact]
    public async Task GenerateVoiceAsync_Cancelled_PropagatesAndSavesNothing()
    {
        // Arrange
        StoryVoiceService service = Service();
        _generator.GenerateAsync(default!, default!, default!, default).ThrowsAsyncForAnyArgs(new OperationCanceledException());
        // Act / Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.GenerateVoiceAsync(_story.Id, "xh", _story.ReviewVersion, CancellationToken.None));
        await _mediaStore.DidNotReceiveWithAnyArgs().SaveAsync(default, default!, default!, default);
    }

    [Fact]
    public async Task GenerateVoiceAsync_SaveConflict_DeletesStoredVoice()
    {
        // Arrange
        StoryVoiceService service = Service();
        _repository.SaveRecordingAsync(default, default!, default, default).ThrowsAsyncForAnyArgs(new StoryConflictException());
        // Act / Assert
        await Assert.ThrowsAsync<StoryConflictException>(() => service.GenerateVoiceAsync(_story.Id, "xh", _story.ReviewVersion, CancellationToken.None));
        await _mediaStore.Received(1).DeleteAsync(Arg.Any<Guid>(), Arg.Is<StoryRecording>(r => r.Extension == ".wav"), CancellationToken.None);
    }
}
