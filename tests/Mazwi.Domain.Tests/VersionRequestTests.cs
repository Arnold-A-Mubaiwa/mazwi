namespace Mazwi.Domain.Tests;

public sealed class VersionRequestTests
{
    private static Story Published()
    {
        Story story = Story.Upload(new StoryDetails("Ìjàpá", "ABS", "Yoruba", "NG", "folktale", "yo"), new StoryRecording("clip.wav", 12), true, TestOwner.Id);
        story.SetTranscript("Ní ayé àtijọ́");
        story.Approve("yo", story.ReviewVersion);
        return story;
    }

    [Fact]
    public void IsApprovedIn_OriginalAndApprovedTranslationsOnly()
    {
        // Arrange
        Story story = Published();
        story.SetTranslation("sn", "Kare kare");
        story.SetTranslation("en", "Long ago");
        story.Approve("en", story.ReviewVersion);
        // Act / Assert
        Assert.True(story.IsApprovedIn("yo"));
        Assert.True(story.IsApprovedIn("en"));
        Assert.False(story.IsApprovedIn("sn"));
        Assert.False(story.IsApprovedIn("zu"));
        story.HideForReview();
        Assert.False(story.IsApprovedIn("yo"));
    }

    [Fact]
    public void For_LanguageNotYetApproved_CreatesRequest()
    {
        // Arrange
        Story story = Published();
        // Act
        VersionRequest request = VersionRequest.For(story, "sn");
        // Assert
        Assert.Equal((story.Id, "sn"), (request.StoryId, request.Language));
    }

    [Theory]
    [InlineData("yo", "already available")]
    [InlineData("de", "languages Mazwi supports")]
    [InlineData("", "languages Mazwi supports")]
    public void For_ApprovedOrUnsupportedLanguage_Throws(string language, string message) =>
        Assert.Contains(message, Assert.Throws<DomainException>(() => VersionRequest.For(Published(), language)).Message);

    [Fact]
    public void For_UnpublishedOrHiddenStory_Throws()
    {
        // Arrange
        Story draft = Story.Upload(new StoryDetails("Draft", "ABS", "Yoruba", "NG", "story", "yo"), new StoryRecording("clip.wav", 12), true, TestOwner.Id);
        Story hidden = Published();
        hidden.HideForReview();
        // Act / Assert
        Assert.Throws<DomainException>(() => VersionRequest.For(draft, "sn"));
        Assert.Throws<DomainException>(() => VersionRequest.For(hidden, "sn"));
        Assert.Throws<ArgumentNullException>(() => VersionRequest.For(null!, "sn"));
    }

    [Theory]
    [InlineData(false, "sn", 1)]
    [InlineData(true, "de", 1)]
    [InlineData(true, "sn", 0)]
    public void VersionRequestTotal_InvalidValues_Throw(bool story, string language, int count) =>
        Assert.Throws<DomainException>(() => new VersionRequestTotal(story ? Guid.NewGuid() : Guid.Empty, language, count));

    [Fact]
    public void VersionRequestTotal_ValidValues_AreKept()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        // Act
        var total = new VersionRequestTotal(id, "sn", 3);
        // Assert
        Assert.Equal((id, "sn", 3), (total.StoryId, total.Language, total.Count));
    }
}
