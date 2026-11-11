using Mazwi.Domain;

namespace Mazwi.Repository.Tests;

public sealed class StoryTwinRepositoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly StoryFingerprint Print = new("Tsuro tricks Gudo at the river.", [0.6f, 0.8f]);

    private async Task<Artist> ArtistAsync()
    {
        Artist artist = Artist.Rehydrate(Guid.NewGuid(), new EmailAddress($"twins-{Guid.NewGuid():N}@mazwi.test"), new PasswordHash("hash"), DateTimeOffset.UtcNow);
        await new ArtistRepository(fixture.DataSource).AddAsync(artist, CancellationToken.None);
        return artist;
    }

    private async Task<Story> StoryAsync(Guid? owner, string language, bool publish = true, string title = "Tsuro na Gudo")
    {
        var repository = new StoryRepository(fixture.DataSource);
        var details = new StoryDetails(title, "Twin artist", "Community", "ZW", "folktale", language);
        Story story = owner is Guid ownerId
            ? Story.Upload(details, new StoryRecording("clip.wav", 19), true, ownerId)
            : Story.Rehydrate(Guid.NewGuid(), details, new StoryRecording("clip.wav", 19), DateTimeOffset.UtcNow);
        await repository.AddAsync(story, CancellationToken.None);
        story.SetTranscript($"Kare kare {Guid.NewGuid():N}");
        await repository.SaveTranscriptAsync(story, CancellationToken.None);
        if (publish)
        {
            await repository.SaveApprovalAsync(story.Id, language, (await repository.GetAsync(story.Id, CancellationToken.None))!.ReviewVersion, false, CancellationToken.None);
        }

        return (await repository.GetAsync(story.Id, CancellationToken.None))!;
    }

    // Other test data shares this database: stories that are not ours are marked as failed at the retry time, so they drop out.
    private async Task<IReadOnlyList<StoryToFingerprint>> PendingAsync(ISet<Guid> ours, DateTimeOffset retryFailedBefore)
    {
        var repository = new StoryTwinRepository(fixture.DataSource);
        var found = new List<StoryToFingerprint>();
        for (int round = 0; round < 100; round++)
        {
            IReadOnlyList<StoryToFingerprint> batch = await repository.ListStoriesToFingerprintAsync(retryFailedBefore, 100, CancellationToken.None);
            found.AddRange(batch.Where(item => ours.Contains(item.StoryId) && found.All(seen => seen.StoryId != item.StoryId)));
            StoryToFingerprint[] others = batch.Where(item => !ours.Contains(item.StoryId)).ToArray();
            if (others.Length == 0)
            {
                break;
            }

            foreach (StoryToFingerprint other in others)
            {
                await repository.SaveFingerprintFailureAsync(other.StoryId, other.SourceHash, retryFailedBefore, CancellationToken.None);
            }
        }

        return found;
    }

    private async Task CleanUpAsync(IEnumerable<Story> stories, IEnumerable<Artist> artists)
    {
        foreach (Story story in stories)
        {
            await new StoryRepository(fixture.DataSource).DeleteAsync(story.Id, CancellationToken.None);
        }

        foreach (Artist artist in artists)
        {
            await new ArtistRepository(fixture.DataSource).DeleteAsync(artist.Id, CancellationToken.None);
        }
    }

    [Fact]
    public async Task ListStoriesToFingerprintAsync_RealPostgres_FindsOnlyTwinnableStoriesWithMissingOrStaleFingerprints()
    {
        // Arrange
        var twins = new StoryTwinRepository(fixture.DataSource);
        var stories = new StoryRepository(fixture.DataSource);
        Artist artist = await ArtistAsync();
        Story published = await StoryAsync(artist.Id, "sn");
        Story draft = await StoryAsync(artist.Id, "sn", publish: false);
        Story unowned = await StoryAsync(null, "sn");
        Story optedOut = await StoryAsync(artist.Id, "sn");
        optedOut.AllowTwins(false);
        await stories.SaveTwinsAllowedAsync(optedOut, CancellationToken.None);
        var ours = new HashSet<Guid> { published.Id, draft.Id, unowned.Id, optedOut.Id };
        try
        {
            // Act
            StoryToFingerprint work = Assert.Single(await PendingAsync(ours, DateTimeOffset.UtcNow));
            // Assert
            Assert.Equal(new StoryToFingerprint(published.Id, "sn", "folktale", published.Transcript!.Value), work);
            await twins.SaveFingerprintAsync(published.Id, work.SourceHash, Print, CancellationToken.None);
            Assert.Empty(await PendingAsync(ours, DateTimeOffset.UtcNow));
            Assert.Contains(await twins.ListCandidatesAsync(CancellationToken.None), candidate => candidate.StoryId == published.Id
                && candidate.OwnerId == artist.Id && candidate.Language == "sn" && candidate.Fingerprint.Vector.SequenceEqual(Print.Vector));

            // A changed transcript makes the fingerprint stale (and unpublishes until approved again).
            published.SetTranscript("Changed words");
            await stories.SaveTranscriptAsync(published, CancellationToken.None);
            await stories.SaveApprovalAsync(published.Id, "sn", (await stories.GetAsync(published.Id, CancellationToken.None))!.ReviewVersion, false, CancellationToken.None);
            Assert.Equal("Changed words", Assert.Single(await PendingAsync(ours, DateTimeOffset.UtcNow)).Text);
            Assert.DoesNotContain(await twins.ListCandidatesAsync(CancellationToken.None), candidate => candidate.StoryId == published.Id);

            // A failure waits until the retry time has passed.
            DateTimeOffset failedAt = DateTimeOffset.UtcNow;
            await twins.SaveFingerprintFailureAsync(published.Id, StoryFingerprint.SourceHash("Changed words"), failedAt, CancellationToken.None);
            Assert.Empty(await PendingAsync(ours, failedAt));
            Assert.Single(await PendingAsync(ours, failedAt.AddMinutes(1)));
        }
        finally { await CleanUpAsync([published, draft, unowned, optedOut], [artist]); }
    }

    [Fact]
    public async Task Suggestions_RealPostgres_RejectionIsFinalAndOnlyEligiblePairsAreShown()
    {
        // Arrange
        var twins = new StoryTwinRepository(fixture.DataSource);
        var stories = new StoryRepository(fixture.DataSource);
        Artist shonaArtist = await ArtistAsync();
        Artist yorubaArtist = await ArtistAsync();
        Story shona = await StoryAsync(shonaArtist.Id, "sn", title: "Tsuro na Gudo");
        Story yoruba = await StoryAsync(yorubaArtist.Id, "yo", title: "Ìjàpá àti Ọ̀bọ");
        Story hausa = await StoryAsync(yorubaArtist.Id, "ha", title: "Kura da Zaki");
        try
        {
            // Act
            await twins.AddSuggestionsAsync([StoryTwin.Suggest(shona.Id, yoruba.Id, 0.91), StoryTwin.Suggest(hausa.Id, shona.Id, 0.85)], CancellationToken.None);
            await twins.AddSuggestionsAsync([StoryTwin.Suggest(yoruba.Id, shona.Id, 0.99)], CancellationToken.None);
            IReadOnlyList<TwinView> views = await twins.ListForOwnerAsync(shonaArtist.Id, CancellationToken.None);
            // Assert
            Assert.Equal([yoruba.Id, hausa.Id], views.Select(view => view.TwinStoryId));
            Assert.Equal(new TwinView(shona.Id, "Tsuro na Gudo", yoruba.Id, "Ìjàpá àti Ọ̀bọ", "Twin artist", "yo", "Community", TwinStatus.Suggested), views[0]);
            Assert.Equal([yoruba.Id, hausa.Id], (await twins.ListForOwnerAsync(yorubaArtist.Id, CancellationToken.None)).Where(view => view.TwinStoryId == shona.Id).Select(view => view.StoryId));
            StoryTwin pair = (await twins.GetVisibleAsync(yoruba.Id, shona.Id, CancellationToken.None))!;
            Assert.Equal(0.91, pair.Similarity, 5);

            pair.Confirm();
            await twins.SaveStatusAsync(pair, CancellationToken.None);
            Assert.Equal(TwinStatus.Confirmed, (await twins.GetVisibleAsync(shona.Id, yoruba.Id, CancellationToken.None))!.Status);
            Assert.Equal(TwinStatus.Confirmed, (await twins.ListForOwnerAsync(shonaArtist.Id, CancellationToken.None)).Single(view => view.TwinStoryId == yoruba.Id).Status);

            // Opting out hides the pair; opting back in shows it again.
            yoruba.AllowTwins(false);
            await stories.SaveTwinsAllowedAsync(yoruba, CancellationToken.None);
            Assert.Null(await twins.GetVisibleAsync(shona.Id, yoruba.Id, CancellationToken.None));
            Assert.DoesNotContain(await twins.ListForOwnerAsync(shonaArtist.Id, CancellationToken.None), view => view.TwinStoryId == yoruba.Id);
            yoruba.AllowTwins(true);
            await stories.SaveTwinsAllowedAsync(yoruba, CancellationToken.None);
            Assert.NotNull(await twins.GetVisibleAsync(shona.Id, yoruba.Id, CancellationToken.None));

            // A hidden story is not shown either.
            hausa.HideForReview();
            await stories.SaveHiddenForReviewAsync(hausa, CancellationToken.None);
            Assert.DoesNotContain(await twins.ListForOwnerAsync(shonaArtist.Id, CancellationToken.None), view => view.TwinStoryId == hausa.Id);

            // Rejection is final: suggesting the pair again changes nothing.
            pair.Reject();
            await twins.SaveStatusAsync(pair, CancellationToken.None);
            await twins.AddSuggestionsAsync([StoryTwin.Suggest(shona.Id, yoruba.Id, 0.99)], CancellationToken.None);
            Assert.Null(await twins.GetVisibleAsync(shona.Id, yoruba.Id, CancellationToken.None));
            Assert.Empty(await twins.ListForOwnerAsync(shonaArtist.Id, CancellationToken.None));
            Assert.Null(await twins.GetVisibleAsync(shona.Id, shona.Id, CancellationToken.None));
        }
        finally { await CleanUpAsync([shona, yoruba, hausa], [shonaArtist, yorubaArtist]); }
    }

    [Fact]
    public async Task DeleteStory_RealPostgres_RemovesItsFingerprintAndPairs()
    {
        // Arrange
        var twins = new StoryTwinRepository(fixture.DataSource);
        Artist first = await ArtistAsync();
        Artist second = await ArtistAsync();
        Story one = await StoryAsync(first.Id, "sn");
        Story two = await StoryAsync(second.Id, "zu");
        try
        {
            await twins.SaveFingerprintAsync(one.Id, StoryFingerprint.SourceHash(one.Transcript!.Value), Print, CancellationToken.None);
            await twins.AddSuggestionsAsync([StoryTwin.Suggest(one.Id, two.Id, 0.9)], CancellationToken.None);
            // Act
            await new StoryRepository(fixture.DataSource).DeleteAsync(one.Id, CancellationToken.None);
            // Assert
            Assert.Empty(await twins.ListForOwnerAsync(second.Id, CancellationToken.None));
            Assert.DoesNotContain(await twins.ListCandidatesAsync(CancellationToken.None), candidate => candidate.StoryId == one.Id);
        }
        finally { await CleanUpAsync([one, two], [first, second]); }
    }

    [Fact]
    public async Task ListStoriesToFingerprintAsync_InvalidLimit_Throws()
    {
        // Act / Assert
        var twins = new StoryTwinRepository(fixture.DataSource);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => twins.ListStoriesToFingerprintAsync(DateTimeOffset.UtcNow, 0, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => twins.ListStoriesToFingerprintAsync(DateTimeOffset.UtcNow, 101, CancellationToken.None));
    }
}
