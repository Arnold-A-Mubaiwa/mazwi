using Mazwi.Domain;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Mazwi.Application.Tests;

public sealed class AiTranslationServiceTests
{
    private readonly IStoryRepository _repository = Substitute.For<IStoryRepository>();
    private readonly ITranslationDrafter _drafter = Substitute.For<ITranslationDrafter>();
    private readonly Story _story = Story.Upload(new StoryDetails("Poem", "Artist", "Shona", "ZW", "poem", "sn"), new StoryRecording("clip.wav", 12), true);

    private StoryDraftService Service(bool approved = true)
    {
        _story.SetTranscript("Original\nWords");
        if (approved)
        {
            _story.Approve("sn", _story.ReviewVersion);
        }

        _repository.GetAsync(_story.Id, Arg.Any<CancellationToken>()).Returns(_story);
        _drafter.DraftAsync(Arg.Any<string>(), "sn", "xh", "poem", Arg.Any<CancellationToken>())
            .Returns(new TranslationDraftResult(new StoryText("Translation"), new TranslationNotes(["Cultural note"])));
        return new StoryDraftService(_repository, _drafter);
    }

    [Fact]
    public async Task DraftTranslationAsync_ApprovedSource_SavesDraftWithNotesAndPassesCancellation()
    {
        // Arrange
        StoryDraftService service = Service();
        Guid version = _story.ReviewVersion;
        using var cancellation = new CancellationTokenSource();
        // Act
        StoryDraftDto? result = await service.DraftTranslationAsync(_story.Id, "xh", version, cancellation.Token);
        // Assert
        TranslationDraftDto draft = Assert.Single(result!.Translations);
        Assert.Equal("draft", draft.Status);
        Assert.Equal("Cultural note", Assert.Single(draft.Notes!));
        await _drafter.Received(1).DraftAsync("Original\nWords", "sn", "xh", "poem", cancellation.Token);
        await _repository.Received(1).SaveAiTranslationAsync(_story.Id, Arg.Is<TranslationDraft>(d => !d.IsApproved), version, cancellation.Token);
    }

    [Fact]
    public async Task DraftTranslationAsync_MissingStory_ReturnsNullWithoutCallingDrafter()
    {
        // Act / Assert
        Assert.Null(await Service().DraftTranslationAsync(Guid.NewGuid(), "xh", Guid.NewGuid(), CancellationToken.None));
        await _drafter.DidNotReceiveWithAnyArgs().DraftAsync(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task DraftTranslationAsync_UnapprovedSource_DoesNotCallDrafter()
    {
        // Arrange
        StoryDraftService service = Service(false);
        // Act / Assert
        await Assert.ThrowsAsync<AiDraftNotAllowedException>(() => service.DraftTranslationAsync(_story.Id, "xh", _story.ReviewVersion, CancellationToken.None));
        await _drafter.DidNotReceiveWithAnyArgs().DraftAsync(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task DraftTranslationAsync_UpstreamFailure_LeavesDraftUntouched()
    {
        // Arrange
        StoryDraftService service = Service();
        _story.SetTranslation("xh", "Existing words");
        Guid version = _story.ReviewVersion;
        _drafter.DraftAsync(default!, default!, default!, default!, default).ThrowsAsyncForAnyArgs(new TranslationDraftUnavailableException());
        // Act / Assert
        await Assert.ThrowsAsync<TranslationDraftUnavailableException>(() => service.DraftTranslationAsync(_story.Id, "xh", version, CancellationToken.None));
        Assert.Equal("Existing words", Assert.Single(_story.Translations).Text.Value);
        Assert.Equal(version, _story.ReviewVersion);
        await _repository.DidNotReceiveWithAnyArgs().SaveAiTranslationAsync(default, default!, default, default);
    }

    [Fact]
    public async Task DraftTranslationAsync_SourceChangesDuringCall_RejectsResult()
    {
        // Arrange
        StoryDraftService service = Service();
        Guid version = _story.ReviewVersion;
        _drafter.DraftAsync(default!, default!, default!, default!, default).ReturnsForAnyArgs(_ =>
        {
            _story.SetTranscript("Changed source");
            return new TranslationDraftResult(new StoryText("Old translation"), new TranslationNotes());
        });
        // Act / Assert
        await Assert.ThrowsAsync<StoryConflictException>(() => service.DraftTranslationAsync(_story.Id, "xh", version, CancellationToken.None));
        await _repository.DidNotReceiveWithAnyArgs().SaveAiTranslationAsync(default, default!, default, default);
    }

    [Fact]
    public async Task DraftTranslationAsync_CancelledDuringCall_DoesNotSave()
    {
        // Arrange
        StoryDraftService service = Service();
        using var cancellation = new CancellationTokenSource();
        _drafter.DraftAsync(default!, default!, default!, default!, default).ReturnsForAnyArgs(_ =>
        {
            cancellation.Cancel();
            return new TranslationDraftResult(new StoryText("Translation"), new TranslationNotes());
        });
        // Act / Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.DraftTranslationAsync(_story.Id, "xh", _story.ReviewVersion, cancellation.Token));
        await _repository.DidNotReceiveWithAnyArgs().SaveAiTranslationAsync(default, default!, default, default);
        Assert.Empty(_story.Translations);
    }
}
