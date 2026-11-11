using Mazwi.Domain;

namespace Mazwi.Repository.Tests;

public sealed class StoryReportRepositoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private async Task<Story> PublishedAsync(StoryRepository repository)
    {
        Story story = Story.Upload(new StoryDetails("Report fixture", "Test artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 19), true, TestOwner.Id);
        await repository.AddAsync(story, CancellationToken.None);
        story.SetTranscript("Kare kare");
        await repository.SaveTranscriptAsync(story, CancellationToken.None);
        story = (await repository.GetAsync(story.Id, CancellationToken.None))!;
        await repository.SaveApprovalAsync(story.Id, "sn", story.ReviewVersion, false, CancellationToken.None);
        return (await repository.GetAsync(story.Id, CancellationToken.None))!;
    }

    [Fact]
    public async Task AddAndList_OpenReports_GroupedByStoryWithReasonsAndNotes()
    {
        // Arrange
        var stories = new StoryRepository(fixture.DataSource);
        var reports = new StoryReportRepository(fixture.DataSource);
        Story story = await PublishedAsync(stories);
        try
        {
            // Act
            await reports.AddAsync(StoryReport.File(story, ReportReason.Harmful, "First note", Now.AddMinutes(-2)), CancellationToken.None);
            await reports.AddAsync(StoryReport.File(story, ReportReason.Harmful, null, Now.AddMinutes(-1)), CancellationToken.None);
            await reports.AddAsync(StoryReport.File(story, ReportReason.Misleading, "Latest note", Now), CancellationToken.None);
            ReportedStory item = (await reports.ListOpenAsync(200, CancellationToken.None)).Single(entry => entry.StoryId == story.Id);
            // Assert
            Assert.Equal(3, item.OpenReports);
            Assert.Equal([ReportReason.Harmful, ReportReason.Misleading], item.Reasons.OrderBy(reason => reason));
            Assert.Equal(["Latest note", "First note"], item.Notes);
            Assert.Equal("Report fixture", item.Title);
        }
        finally { await stories.DeleteAsync(story.Id, CancellationToken.None); }
    }

    [Fact]
    public async Task ResolveAndHide_Story_LeavesQueueAndFeedAsChosen()
    {
        // Arrange
        var stories = new StoryRepository(fixture.DataSource);
        var reports = new StoryReportRepository(fixture.DataSource);
        Story story = await PublishedAsync(stories);
        try
        {
            await reports.AddAsync(StoryReport.File(story, ReportReason.NoPermission, null, Now), CancellationToken.None);
            Guid version = story.ReviewVersion;
            // Act
            story.HideForReview();
            await stories.SaveHiddenForReviewAsync(story, CancellationToken.None);
            bool hiddenInQueue = (await reports.ListOpenAsync(200, CancellationToken.None)).Single(entry => entry.StoryId == story.Id).HiddenForReview;
            IReadOnlyList<Guid> feed = await stories.ListIdsAsync(true, 0, 50, CancellationToken.None);
            await reports.ResolveAsync(story.Id, Now, CancellationToken.None);
            Story loaded = (await stories.GetAsync(story.Id, CancellationToken.None))!;
            // Assert
            Assert.True(hiddenInQueue);
            Assert.DoesNotContain(story.Id, feed);
            Assert.DoesNotContain(await reports.ListOpenAsync(200, CancellationToken.None), entry => entry.StoryId == story.Id);
            Assert.True(loaded.HiddenForReview);
            Assert.Equal(version, loaded.ReviewVersion);
            loaded.RestoreAfterReview();
            await stories.SaveHiddenForReviewAsync(loaded, CancellationToken.None);
            Assert.Contains(story.Id, await stories.ListIdsAsync(true, 0, 50, CancellationToken.None));
        }
        finally { await stories.DeleteAsync(story.Id, CancellationToken.None); }
    }

    [Fact]
    public async Task ListOpenAsync_InvalidLimit_Throws()
    {
        // Act / Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new StoryReportRepository(fixture.DataSource).ListOpenAsync(0, CancellationToken.None));
    }
}
