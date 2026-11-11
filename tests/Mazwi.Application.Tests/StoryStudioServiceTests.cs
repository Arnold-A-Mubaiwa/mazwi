using Mazwi.Domain;
using NSubstitute;

namespace Mazwi.Application.Tests;

public sealed class StoryStudioServiceTests
{
    [Fact]
    public async Task GetPageAsync_OwnedDrafts_ReturnsSummariesAndPassesOwnerAndPagination()
    {
        // Arrange
        var repository = Substitute.For<IStoryRepository>();
        Story story = Story.Upload(new StoryDetails("Studio story", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 19), true, TestOwner.Id);
        repository.ListOwnedIdsAsync(TestOwner.Id, 20, 1, Arg.Any<CancellationToken>()).Returns(new[] { story.Id });
        repository.GetAsync(story.Id, Arg.Any<CancellationToken>()).Returns(story);
        using var cancellation = new CancellationTokenSource();
        // Act
        StoryStudioPageDto page = await new StoryStudioService(repository).GetPageAsync(TestOwner.Id, 20, 1, cancellation.Token);
        // Assert
        Assert.Equal("unpublished", Assert.Single(page.Items).Status); Assert.Equal(21, page.NextOffset);
        await repository.Received(1).ListOwnedIdsAsync(TestOwner.Id, 20, 1, cancellation.Token);
        await repository.Received(1).GetAsync(story.Id, cancellation.Token);
    }

    [Fact]
    public async Task GetPageAsync_RemovedOrEmptyRecords_ReturnsEmptyPage()
    {
        // Arrange
        var repository = Substitute.For<IStoryRepository>();
        repository.ListOwnedIdsAsync(TestOwner.Id, 0, 20, Arg.Any<CancellationToken>()).Returns(new[] { Guid.NewGuid() });
        // Act
        StoryStudioPageDto page = await new StoryStudioService(repository).GetPageAsync(TestOwner.Id, 0, 20, CancellationToken.None);
        // Assert
        Assert.Empty(page.Items); Assert.Null(page.NextOffset);
    }
}
