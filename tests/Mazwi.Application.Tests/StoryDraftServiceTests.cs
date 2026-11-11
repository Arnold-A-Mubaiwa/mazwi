using Mazwi.Domain;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Mazwi.Application.Tests;

public sealed class StoryDraftServiceTests
{
    private readonly IStoryRepository _repository = Substitute.For<IStoryRepository>();
    private readonly Story _story = Story.Upload(new StoryDetails("Story", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 12), true, TestOwner.Id);

    private StoryDraftService Service()
    {
        _repository.GetAsync(_story.Id, Arg.Any<CancellationToken>()).Returns(_story);
        return new StoryDraftService(_repository);
    }

    [Fact]
    public async Task SaveTranscriptAsync_ValidText_EnforcesRulesAndPassesCancellation()
    {
        // Arrange
        StoryDraftService service = Service();
        using var cancellation = new CancellationTokenSource();
        // Act
        StoryDraftDto? result = await service.SaveTranscriptAsync(_story.Id, " Words\nMore words ", cancellation.Token);
        // Assert
        Assert.NotNull(result);
        Assert.Equal("Words\nMore words", result.Transcript);
        Assert.Equal("unpublished", result.Status);
        await _repository.Received(2).GetAsync(_story.Id, cancellation.Token);
        await _repository.Received(1).SaveTranscriptAsync(_story, cancellation.Token);
    }

    [Fact]
    public async Task SaveTranslationAsync_ValidDraft_SavesOnlySelectedLanguage()
    {
        // Arrange
        StoryDraftService service = Service();
        _story.SetTranscript("Original");
        using var cancellation = new CancellationTokenSource();
        // Act
        StoryDraftDto? result = await service.SaveTranslationAsync(_story.Id, "xh", " Translation ", cancellation.Token);
        // Assert
        Assert.NotNull(result);
        Assert.Equal(new TranslationDraftDto("xh", "Translation", "draft"), Assert.Single(result.Translations));
        await _repository.Received(1).SaveTranslationAsync(_story.Id, Arg.Is<TranslationDraft>(draft => draft.Language == "xh" && draft.Text.Value == "Translation"), cancellation.Token);
        await _repository.DidNotReceiveWithAnyArgs().SaveTranscriptAsync(default!, default);
    }

    [Fact]
    public async Task SaveTranscriptAsync_InvalidText_DoesNotWrite()
    {
        // Arrange
        StoryDraftService service = Service();
        // Act / Assert
        await Assert.ThrowsAsync<DomainException>(() => service.SaveTranscriptAsync(_story.Id, " ", CancellationToken.None));
        await _repository.DidNotReceiveWithAnyArgs().SaveTranscriptAsync(default!, default);
    }

    [Fact]
    public async Task SaveTranslationAsync_NoOriginal_DoesNotWrite()
    {
        // Arrange
        StoryDraftService service = Service();
        // Act / Assert
        await Assert.ThrowsAsync<DomainException>(() => service.SaveTranslationAsync(_story.Id, "xh", "Words", CancellationToken.None));
        await _repository.DidNotReceiveWithAnyArgs().SaveTranslationAsync(default, default!, default);
    }

    [Theory]
    [InlineData("sn")]
    [InlineData("xx")]
    public async Task SaveTranslationAsync_InvalidTarget_DoesNotWrite(string language)
    {
        // Arrange
        StoryDraftService service = Service();
        _story.SetTranscript("Original");
        // Act / Assert
        await Assert.ThrowsAsync<DomainException>(() => service.SaveTranslationAsync(_story.Id, language, "Words", CancellationToken.None));
        await _repository.DidNotReceiveWithAnyArgs().SaveTranslationAsync(default, default!, default);
    }

    [Fact]
    public async Task AllMethods_MissingStory_ReturnNullWithoutWrites()
    {
        // Arrange
        var service = new StoryDraftService(_repository);
        Guid missing = Guid.NewGuid();
        // Act / Assert
        Assert.Null(await service.GetAsync(missing, CancellationToken.None));
        Assert.Null(await service.SaveTranscriptAsync(missing, "Original", CancellationToken.None));
        Assert.Null(await service.SaveTranslationAsync(missing, "xh", "Translation", CancellationToken.None));
        await _repository.DidNotReceiveWithAnyArgs().SaveTranscriptAsync(default!, default);
        await _repository.DidNotReceiveWithAnyArgs().SaveTranslationAsync(default, default!, default);
    }

    [Fact]
    public async Task GetAsync_ExistingDrafts_ReturnsAllSavedText()
    {
        // Arrange
        StoryDraftService service = Service();
        _story.SetTranscript("Original");
        _story.SetTranslation("xh", "Words");
        // Act / Assert
        StoryDraftDto? result = await service.GetAsync(_story.Id, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal("Original", result.Transcript);
        Assert.Single(result.Translations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveTranscriptAsync_RepositoryErrorOrCancellation_PropagatesFailure(bool cancelled)
    {
        // Arrange
        StoryDraftService service = Service();
        Exception failure = cancelled ? new OperationCanceledException() : new IOException("database failed");
        _repository.SaveTranscriptAsync(default!, default).ThrowsAsyncForAnyArgs(failure);
        // Act / Assert
        Assert.Same(failure, await Record.ExceptionAsync(() => service.SaveTranscriptAsync(_story.Id, "Original", CancellationToken.None)));
    }

    [Fact]
    public async Task SaveTranslationAsync_RepositoryError_PropagatesFailure()
    {
        // Arrange
        StoryDraftService service = Service();
        _story.SetTranscript("Original");
        _repository.SaveTranslationAsync(default, default!, default).ThrowsAsyncForAnyArgs(new IOException("database failed"));
        // Act / Assert
        await Assert.ThrowsAsync<IOException>(() => service.SaveTranslationAsync(_story.Id, "xh", "Words", CancellationToken.None));
    }
}
