using Mazwi.Domain;

namespace Mazwi.Repository.Tests;

public sealed class StoryVersionRequestRepositoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private async Task<Artist> ArtistAsync()
    {
        Artist artist = Artist.Rehydrate(Guid.NewGuid(), new EmailAddress($"requests-{Guid.NewGuid():N}@mazwi.test"), new PasswordHash("hash"), DateTimeOffset.UtcNow);
        await new ArtistRepository(fixture.DataSource).AddAsync(artist, CancellationToken.None);
        return artist;
    }

    private async Task<Story> PublishedAsync(Guid owner, string language)
    {
        var repository = new StoryRepository(fixture.DataSource);
        Story story = Story.Upload(new StoryDetails("Ìjàpá", "ABS", "Yoruba", "NG", "folktale", language), new StoryRecording("clip.wav", 19), true, owner);
        await repository.AddAsync(story, CancellationToken.None);
        story.SetTranscript("Ní ayé àtijọ́");
        await repository.SaveTranscriptAsync(story, CancellationToken.None);
        await repository.SaveApprovalAsync(story.Id, language, (await repository.GetAsync(story.Id, CancellationToken.None))!.ReviewVersion, false, CancellationToken.None);
        return (await repository.GetAsync(story.Id, CancellationToken.None))!;
    }

    [Fact]
    public async Task AddAndList_RealPostgres_CountsPerLanguageUntilTheArtistApprovesIt()
    {
        // Arrange
        var requests = new StoryVersionRequestRepository(fixture.DataSource);
        var stories = new StoryRepository(fixture.DataSource);
        Artist owner = await ArtistAsync();
        Artist someoneElse = await ArtistAsync();
        Story story = await PublishedAsync(owner.Id, "yo");
        try
        {
            // Act
            await requests.AddAsync(VersionRequest.For(story, "sn"), CancellationToken.None);
            await requests.AddAsync(VersionRequest.For(story, "sn"), CancellationToken.None);
            await requests.AddAsync(VersionRequest.For(story, "zu"), CancellationToken.None);
            IReadOnlyList<VersionRequestTotal> totals = await requests.ListForOwnerAsync(owner.Id, CancellationToken.None);
            // Assert
            Assert.Equal([(story.Id, "sn", 2), (story.Id, "zu", 1)], totals.Select(total => (total.StoryId, total.Language, total.Count)));
            Assert.Empty(await requests.ListForOwnerAsync(someoneElse.Id, CancellationToken.None));

            // Once the artist approves a Shona version, the Shona requests stop showing.
            await stories.SaveTranslationAsync(story.Id, story.SetTranslation("sn", "Kare kare"), CancellationToken.None);
            await stories.SaveApprovalAsync(story.Id, "sn", (await stories.GetAsync(story.Id, CancellationToken.None))!.ReviewVersion, false, CancellationToken.None);
            Assert.Equal(["zu"], (await requests.ListForOwnerAsync(owner.Id, CancellationToken.None)).Select(total => total.Language));

            // Deleting the story deletes its requests.
            await stories.DeleteAsync(story.Id, CancellationToken.None);
            Assert.Empty(await requests.ListForOwnerAsync(owner.Id, CancellationToken.None));
        }
        finally
        {
            await stories.DeleteAsync(story.Id, CancellationToken.None);
            await new ArtistRepository(fixture.DataSource).DeleteAsync(owner.Id, CancellationToken.None);
            await new ArtistRepository(fixture.DataSource).DeleteAsync(someoneElse.Id, CancellationToken.None);
        }
    }

    [Fact]
    public async Task ListConfirmedTwinIdsAsync_RealPostgres_OnlyConfirmedAndStillTwinnable()
    {
        // Arrange
        var twins = new StoryTwinRepository(fixture.DataSource);
        var stories = new StoryRepository(fixture.DataSource);
        Artist first = await ArtistAsync();
        Artist second = await ArtistAsync();
        Story shona = await PublishedAsync(first.Id, "sn");
        Story yoruba = await PublishedAsync(second.Id, "yo");
        Story hausa = await PublishedAsync(second.Id, "ha");
        try
        {
            await twins.AddSuggestionsAsync([StoryTwin.Suggest(shona.Id, yoruba.Id, 0.9), StoryTwin.Suggest(shona.Id, hausa.Id, 0.95)], CancellationToken.None);
            StoryTwin confirmed = (await twins.GetVisibleAsync(shona.Id, yoruba.Id, CancellationToken.None))!;
            confirmed.Confirm();
            await twins.SaveStatusAsync(confirmed, CancellationToken.None);
            // Act
            IReadOnlyList<Guid> fromShona = await twins.ListConfirmedTwinIdsAsync(shona.Id, CancellationToken.None);
            // Assert
            Assert.Equal([yoruba.Id], fromShona);
            Assert.Equal([shona.Id], await twins.ListConfirmedTwinIdsAsync(yoruba.Id, CancellationToken.None));
            Assert.Empty(await twins.ListConfirmedTwinIdsAsync(hausa.Id, CancellationToken.None));
            yoruba.HideForReview();
            await stories.SaveHiddenForReviewAsync(yoruba, CancellationToken.None);
            Assert.Empty(await twins.ListConfirmedTwinIdsAsync(shona.Id, CancellationToken.None));
        }
        finally
        {
            foreach (Story story in new[] { shona, yoruba, hausa })
            {
                await stories.DeleteAsync(story.Id, CancellationToken.None);
            }

            await new ArtistRepository(fixture.DataSource).DeleteAsync(first.Id, CancellationToken.None);
            await new ArtistRepository(fixture.DataSource).DeleteAsync(second.Id, CancellationToken.None);
        }
    }
}
