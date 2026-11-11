using Mazwi.Domain;
using NSubstitute;

namespace Mazwi.Application.Tests;

public sealed class SpeakerCheckedServiceTests
{
    private readonly IStoryRepository _repository = Substitute.For<IStoryRepository>();
    private readonly IStoryMediaStore _media = Substitute.For<IStoryMediaStore>();
    private readonly Story _story = Story.Upload(new StoryDetails("Story", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 19), true, TestOwner.Id);

    public SpeakerCheckedServiceTests()
    {
        _story.SetTranscript("Kare kare"); _story.SetTranslation("xh", "Kudala");
        _story.Approve("sn", _story.ReviewVersion);
        _repository.GetAsync(_story.Id, Arg.Any<CancellationToken>()).Returns(_story);
        _media.OpenReadAsync(_story.Id, _story.Recording, Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult<Stream?>(new MemoryStream(new byte[19])));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ApproveAsync_Translation_PassesSpeakerCheckToRepositoryAndDraft(bool speakerChecked)
    {
        // Arrange
        var service = new StoryReviewService(_repository, _media); Guid version = _story.ReviewVersion;
        // Act
        StoryDraftDto? result = await service.ApproveAsync(_story.Id, "xh", version, speakerChecked, CancellationToken.None);
        // Assert
        await _repository.Received(1).SaveApprovalAsync(_story.Id, "xh", version, speakerChecked, CancellationToken.None);
        Assert.Equal(speakerChecked, Assert.Single(result!.Translations).SpeakerChecked);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetAsync_ApprovedTranslation_ExposesSpeakerCheck(bool speakerChecked)
    {
        // Arrange
        _story.Approve("xh", _story.ReviewVersion, speakerChecked);
        var service = new StoryFeedService(_repository, _media);
        // Act
        StoryViewDto? item = await service.GetAsync(_story.Id, "xh", CancellationToken.None);
        // Assert
        Assert.Equal("xh", item!.TextLanguage); Assert.Equal(speakerChecked, item.SpeakerChecked);
    }

    [Fact]
    public async Task GetAsync_Original_IsAlwaysSpeakerChecked()
    {
        // Arrange
        var service = new StoryFeedService(_repository, _media);
        // Act
        StoryViewDto? item = await service.GetAsync(_story.Id, "sn", CancellationToken.None);
        // Assert
        Assert.True(item!.SpeakerChecked);
    }
}
