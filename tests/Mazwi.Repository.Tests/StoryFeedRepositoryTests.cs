using Mazwi.Domain;

namespace Mazwi.Repository.Tests;

public sealed class StoryFeedRepositoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task ListIdsAsync_RealPostgres_IncludesOnlyApprovedOriginals()
    {
        // Arrange
        var repository = new StoryRepository(fixture.DataSource);
        Story approved = Story.Upload(new StoryDetails("Feed fixture", "Test artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 19), true);
        Story draft = Story.Upload(approved.Details, approved.Recording, true);
        await repository.AddAsync(approved, CancellationToken.None); await repository.AddAsync(draft, CancellationToken.None);
        try
        {
            approved.SetTranscript("Original"); await repository.SaveTranscriptAsync(approved, CancellationToken.None);
            approved = (await repository.GetAsync(approved.Id, CancellationToken.None))!;
            await repository.SaveApprovalAsync(approved.Id, "sn", approved.ReviewVersion, CancellationToken.None);
            // Act
            IReadOnlyList<Guid> ids = await repository.ListIdsAsync(true, 0, 50, CancellationToken.None);
            // Assert
            Assert.Contains(approved.Id, ids); Assert.DoesNotContain(draft.Id, ids);
            Assert.Contains(draft.Id, await repository.ListIdsAsync(false, 0, 50, CancellationToken.None));
            approved.SetTranscript("Changed"); await repository.SaveTranscriptAsync(approved, CancellationToken.None);
            Assert.DoesNotContain(approved.Id, await repository.ListIdsAsync(true, 0, 50, CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.ListIdsAsync(true, 0, 0, CancellationToken.None));
        }
        finally { await repository.DeleteAsync(approved.Id, CancellationToken.None); await repository.DeleteAsync(draft.Id, CancellationToken.None); }
    }
}
