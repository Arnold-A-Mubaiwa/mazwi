namespace Mazwi.Domain.Tests;

public sealed class StoryTwinTests
{
    private static readonly Guid Low = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid High = Guid.Parse("ffffffff-0000-0000-0000-000000000001");

    private static StoryFingerprint Print(params float[] vector) => new("Tsuro tricks Gudo.", vector);

    [Fact]
    public void Suggest_EitherOrder_StoresThePairOnceInFixedOrder()
    {
        // Act
        StoryTwin forward = StoryTwin.Suggest(Low, High, 0.9);
        StoryTwin backward = StoryTwin.Suggest(High, Low, 0.9);
        // Assert
        Assert.Equal((Low, High), (forward.StoryA, forward.StoryB));
        Assert.Equal((Low, High), (backward.StoryA, backward.StoryB));
        Assert.Equal(TwinStatus.Suggested, forward.Status);
        Assert.True(forward.Involves(High));
        Assert.Equal(High, forward.TwinOf(Low));
        Assert.Equal(Low, forward.TwinOf(High));
        Assert.Throws<DomainException>(() => forward.TwinOf(Guid.NewGuid()));
    }

    [Fact]
    public void Pair_TextOrder_MatchesPostgresUuidOrder()
    {
        // Arrange: PostgreSQL orders uuids by their bytes, which is the order of their text form.
        Guid first = Guid.Parse("0a000000-0000-0000-0000-0000000000ff");
        Guid second = Guid.Parse("0b000000-0000-0000-0000-000000000000");
        // Act / Assert
        Assert.Equal((first, second), StoryTwin.Pair(second, first));
    }

    [Theory]
    [InlineData(1.5)]
    [InlineData(-1.5)]
    [InlineData(double.NaN)]
    public void Suggest_InvalidSimilarity_Throws(double similarity) =>
        Assert.Throws<DomainException>(() => StoryTwin.Suggest(Low, High, similarity));

    [Fact]
    public void SuggestAndRehydrate_InvalidPairs_Throw()
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => StoryTwin.Suggest(Low, Low, 0.9));
        Assert.Throws<DomainException>(() => StoryTwin.Suggest(Guid.Empty, High, 0.9));
        Assert.Throws<DomainException>(() => StoryTwin.Rehydrate(High, Low, 0.9, TwinStatus.Suggested));
        Assert.Throws<DomainException>(() => StoryTwin.Rehydrate(Low, High, 0.9, (TwinStatus)9));
    }

    [Fact]
    public void ConfirmAndReject_Lifecycle_RejectionIsFinal()
    {
        // Arrange
        StoryTwin twin = StoryTwin.Suggest(Low, High, 0.9);
        // Act
        twin.Confirm();
        twin.Confirm();
        Assert.Equal(TwinStatus.Confirmed, twin.Status);
        twin.Reject();
        // Assert
        Assert.Equal(TwinStatus.Rejected, twin.Status);
        Assert.Throws<DomainException>(() => twin.Confirm());
        twin.Reject();
        Assert.Equal(TwinStatus.Rejected, twin.Status);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    public void TwinThreshold_OutOfRange_Throws(double value) =>
        Assert.Throws<DomainException>(() => new TwinThreshold(value));

    [Theory]
    [InlineData(0.01)]
    [InlineData(0.8)]
    [InlineData(1.0)]
    public void TwinThreshold_InRange_IsKept(double value) => Assert.Equal(value, new TwinThreshold(value).Value);

    [Fact]
    public void StoryFingerprint_Vector_IsNormalisedAndComparedByCosine()
    {
        // Arrange
        StoryFingerprint a = Print(3, 4);
        StoryFingerprint b = Print(6, 8);
        StoryFingerprint c = Print(-4, 3);
        // Act / Assert
        Assert.Equal([0.6f, 0.8f], a.Vector);
        Assert.Equal(1, a.SimilarityTo(b), 5);
        Assert.Equal(0, a.SimilarityTo(c), 5);
        Assert.Equal(0, a.SimilarityTo(Print(1, 2, 3)));
    }

    [Fact]
    public void StoryFingerprint_InvalidInput_Throws()
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => new StoryFingerprint(" ", [1f]));
        Assert.Throws<DomainException>(() => new StoryFingerprint(new string('a', StoryFingerprint.MaxGistLength + 1), [1f]));
        Assert.Throws<DomainException>(() => new StoryFingerprint("Gist\u0000", [1f]));
        Assert.Throws<DomainException>(() => new StoryFingerprint("Gist", []));
        Assert.Throws<DomainException>(() => new StoryFingerprint("Gist", [0f, 0f]));
        Assert.Throws<DomainException>(() => new StoryFingerprint("Gist", [float.NaN]));
        Assert.Throws<DomainException>(() => new StoryFingerprint("Gist", new float[StoryFingerprint.MaxDimensions + 1]));
        Assert.Equal("Line one\nLine two", new StoryFingerprint(" Line one\nLine two ", [1f]).Gist);
    }

    [Fact]
    public void SourceHash_Text_IsLowercaseSha256OfUtf8()
    {
        // Act / Assert
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", StoryFingerprint.SourceHash("abc"));
        Assert.Equal(StoryFingerprint.SourceHash("Ní ayé"), new StoryToFingerprint(Low, "yo", "story", "Ní ayé").SourceHash);
    }

    [Fact]
    public void Match_Candidates_OnlyOtherArtistsOtherLanguagesAboveThresholdBestFirst()
    {
        // Arrange
        Guid owner = Guid.NewGuid();
        var story = new TwinCandidate(Guid.NewGuid(), owner, "sn", Print(1, 0));
        var best = new TwinCandidate(Guid.NewGuid(), Guid.NewGuid(), "yo", Print(1, 0.1f));
        var good = new TwinCandidate(Guid.NewGuid(), Guid.NewGuid(), "ha", Print(1, 0.4f));
        var sameArtist = new TwinCandidate(Guid.NewGuid(), owner, "yo", Print(1, 0));
        var sameLanguage = new TwinCandidate(Guid.NewGuid(), Guid.NewGuid(), "sn", Print(1, 0));
        var different = new TwinCandidate(Guid.NewGuid(), Guid.NewGuid(), "zu", Print(0, 1));
        // Act
        IReadOnlyList<StoryTwin> twins = TwinMatcher.Match(story, [story, good, sameArtist, sameLanguage, different, best], new TwinThreshold(0.9));
        // Assert
        Assert.Equal([best.StoryId, good.StoryId], twins.Select(twin => twin.TwinOf(story.StoryId)));
        Assert.All(twins, twin => Assert.Equal(TwinStatus.Suggested, twin.Status));
    }

    [Fact]
    public void Match_ManyCloseStories_KeepsTheBestFive()
    {
        // Arrange
        var story = new TwinCandidate(Guid.NewGuid(), Guid.NewGuid(), "sn", Print(1, 0));
        TwinCandidate[] others = Enumerable.Range(0, 8).Select(i => new TwinCandidate(Guid.NewGuid(), Guid.NewGuid(), "yo", Print(1, i / 100f))).ToArray();
        // Act
        IReadOnlyList<StoryTwin> twins = TwinMatcher.Match(story, others, new TwinThreshold(0.5));
        // Assert
        Assert.Equal(TwinMatcher.MaxSuggestionsPerStory, twins.Count);
        Assert.Equal(others.Take(5).Select(other => other.StoryId), twins.Select(twin => twin.TwinOf(story.StoryId)));
    }

    [Fact]
    public void Story_TwinsOptOut_DefaultsToAllowedAndControlsEligibility()
    {
        // Arrange
        Story story = Story.Upload(new StoryDetails("Tsuro na Gudo", "Artist", "Shona", "ZW", "folktale", "sn"), new StoryRecording("clip.wav", 12), true, TestOwner.Id);
        Assert.True(story.TwinsAllowed);
        Assert.False(story.CanBeTwinned);
        story.SetTranscript("Kare kare");
        story.Approve("sn", story.ReviewVersion);
        Assert.True(story.CanBeTwinned);
        // Act
        story.AllowTwins(false);
        // Assert
        Assert.False(story.CanBeTwinned);
        story.AllowTwins(true);
        story.HideForReview();
        Assert.False(story.CanBeTwinned);
        Story unowned = Story.Rehydrate(Guid.NewGuid(), story.Details, story.Recording, DateTimeOffset.UtcNow, "Kare", originalApproved: true);
        Assert.False(unowned.CanBeTwinned);
        Assert.False(Story.Rehydrate(Guid.NewGuid(), story.Details, story.Recording, DateTimeOffset.UtcNow, ownerId: TestOwner.Id, twinsAllowed: false).TwinsAllowed);
    }
}
