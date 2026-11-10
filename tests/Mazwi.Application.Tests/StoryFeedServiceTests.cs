using Mazwi.Domain;
using NSubstitute;

namespace Mazwi.Application.Tests;

public sealed class StoryFeedServiceTests
{
    private readonly IStoryRepository _repository = Substitute.For<IStoryRepository>();
    private readonly IStoryMediaStore _media = Substitute.For<IStoryMediaStore>();
    private readonly Story _story = Story.Upload(new StoryDetails("Story", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 19), true);
    private StoryFeedService Service(bool approve = true)
    {
        _story.SetTranscript("Original"); _story.SetTranslation("xh", "Translated");
        if (approve)
        {
            _story.Approve("sn", _story.ReviewVersion);
        }

        _repository.GetAsync(_story.Id, Arg.Any<CancellationToken>()).Returns(_story);
        _repository.ListIdsAsync(true, 0, 20, Arg.Any<CancellationToken>()).Returns(new[] { _story.Id });
        _media.OpenReadAsync(_story.Id, _story.Recording, Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult<Stream?>(new MemoryStream(new byte[19])));
        return new(_repository, _media);
    }

    [Fact]
    public async Task GetPageAsync_ApprovedOriginal_ReturnsCreditsAndHonestLanguageFallback()
    {
        // Arrange
        StoryFeedService service = Service();
        // Act
        StoryFeedPageDto page = await service.GetPageAsync("xh", 0, 20, CancellationToken.None);
        // Assert
        StoryViewDto item = Assert.Single(page.Items); Assert.Equal("sn", item.TextLanguage); Assert.Equal("sn", item.AudioLanguage);
        Assert.Equal("Artist", item.ArtistName); Assert.Equal("Shona", item.Community); Assert.Null(page.NextOffset);
    }

    [Fact]
    public async Task GetAsync_DraftOrMissingFile_DoesNotExposeStory()
    {
        // Arrange
        StoryFeedService service = Service(false);
        // Act / Assert
        Assert.Null(await service.GetAsync(_story.Id, "sn", CancellationToken.None));
        _story.Approve("sn", _story.ReviewVersion);
        _media.OpenReadAsync(_story.Id, _story.Recording, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Stream?>(null));
        Assert.Null(await service.GetAsync(_story.Id, "sn", CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_MissingTranslatedRecording_FallsBackWithApprovedText()
    {
        // Arrange
        StoryFeedService service = Service();
        _story.AttachRecording("xh", new TranslatedRecording(Guid.NewGuid(), new StoryRecording("voice.mp3", 19)), true);
        _story.Approve("xh", _story.ReviewVersion);
        // Act
        StoryViewDto? item = await service.GetAsync(_story.Id, "xh", CancellationToken.None);
        // Assert
        Assert.NotNull(item); Assert.Equal("xh", item.TextLanguage); Assert.Equal("sn", item.AudioLanguage); Assert.Equal("Translated", item.Text);
    }

    [Fact]
    public async Task GetRecordingAsync_StaleVersionOrRevokedApproval_RejectsPlayback()
    {
        // Arrange
        StoryFeedService service = Service(); Guid version = _story.ReviewVersion;
        // Act / Assert
        await Assert.ThrowsAsync<StoryConflictException>(() => service.GetRecordingAsync(_story.Id, "sn", Guid.NewGuid(), false, CancellationToken.None));
        RecordingPlaybackDto? current = await service.GetRecordingAsync(_story.Id, "sn", version, false, CancellationToken.None);
        Assert.NotNull(current); await current.Content.DisposeAsync();
        _story.SetTranscript("Changed");
        Assert.Null(await service.GetRecordingAsync(_story.Id, "sn", version, false, CancellationToken.None));
    }

    [Fact]
    public async Task GetPageAsync_InvalidLanguage_RejectsBeforeQuery()
    {
        // Arrange
        var service = new StoryFeedService(_repository, _media);
        // Act / Assert
        await Assert.ThrowsAsync<DomainException>(() => service.GetPageAsync("xx", 0, 20, CancellationToken.None));
        await _repository.DidNotReceiveWithAnyArgs().ListIdsAsync(default, default, default, default);
    }
}
