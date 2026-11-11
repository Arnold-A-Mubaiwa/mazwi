namespace Mazwi.Domain.Tests;

public sealed class StoryApprovalTests
{
    private static Story StoryWithText()
    {
        Story story = Story.Upload(new StoryDetails("Story", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 19), true, TestOwner.Id);
        story.SetTranscript("Original"); story.SetTranslation("xh", "Xhosa draft"); story.SetTranslation("en", "English draft");
        return story;
    }

    [Fact]
    public void Approve_CurrentContent_PublishesOnlyExplicitlyApprovedLanguages()
    {
        // Arrange
        Story story = StoryWithText();
        // Act
        story.Approve("sn", story.ReviewVersion); story.Approve("xh", story.ReviewVersion);
        // Assert
        Assert.Equal("published", story.Status);
        Assert.True(story.OriginalApproved);
        Assert.True(story.Translations.Single(d => d.Language == "xh").IsApproved);
        Assert.False(story.Translations.Single(d => d.Language == "en").IsApproved);
    }

    [Fact]
    public void SetTranscript_ChangedOriginal_RevokesEveryApprovalButPreservesText()
    {
        // Arrange
        Story story = StoryWithText(); story.Approve("sn", story.ReviewVersion); story.Approve("xh", story.ReviewVersion);
        Guid previous = story.ReviewVersion;
        // Act
        story.SetTranscript("Changed original");
        // Assert
        Assert.False(story.OriginalApproved); Assert.All(story.Translations, d => Assert.False(d.IsApproved));
        Assert.Equal(2, story.Translations.Count); Assert.NotEqual(previous, story.ReviewVersion);
        Assert.Throws<StoryConflictException>(() => story.Approve("sn", previous));
    }

    [Fact]
    public void SetTranslation_ChangedOneLanguage_RevokesOnlyThatApproval()
    {
        // Arrange
        Story story = StoryWithText(); story.Approve("sn", story.ReviewVersion); story.Approve("xh", story.ReviewVersion); story.Approve("en", story.ReviewVersion);
        // Act
        story.SetTranslation("xh", "Changed translation");
        // Assert
        Assert.True(story.OriginalApproved); Assert.True(story.Translations.Single(d => d.Language == "en").IsApproved);
        Assert.False(story.Translations.Single(d => d.Language == "xh").IsApproved);
    }

    [Fact]
    public void SetText_UnchangedContent_PreservesApprovalsAndVersion()
    {
        // Arrange
        Story story = StoryWithText(); story.Approve("sn", story.ReviewVersion); story.Approve("xh", story.ReviewVersion);
        Guid version = story.ReviewVersion;
        // Act
        story.SetTranscript(" Original "); story.SetTranslation("xh", "Xhosa draft");
        // Assert
        Assert.Equal(version, story.ReviewVersion); Assert.True(story.OriginalApproved);
        Assert.True(story.Translations.Single(d => d.Language == "xh").IsApproved);
    }

    [Fact]
    public void AttachRecording_PermittedReplacement_RequiresApprovalAgain()
    {
        // Arrange
        Story story = StoryWithText(); story.Approve("sn", story.ReviewVersion); story.Approve("xh", story.ReviewVersion);
        var recording = new TranslatedRecording(Guid.NewGuid(), new StoryRecording("voice.m4a", 100));
        // Act
        story.AttachRecording("xh", recording, true);
        // Assert
        Assert.Equal(recording, story.Translations.Single(d => d.Language == "xh").Recording);
        Assert.False(story.Translations.Single(d => d.Language == "xh").IsApproved); Assert.True(story.OriginalApproved);
        Assert.Throws<DomainException>(() => story.AttachRecording("en", recording, false));
        Assert.Throws<DomainException>(() => story.AttachRecording("sw", recording, true));
    }

    [Fact]
    public void Approve_MissingPrerequisites_RejectsApproval()
    {
        // Arrange
        Story story = StoryWithText();
        // Act / Assert
        Assert.Throws<DomainException>(() => story.Approve("xh", story.ReviewVersion));
        story.Approve("sn", story.ReviewVersion);
        Assert.Throws<DomainException>(() => story.Approve("sw", story.ReviewVersion));
        Story empty = Story.Upload(story.Details, story.Recording, true, TestOwner.Id);
        Assert.Throws<DomainException>(() => empty.Approve("sn", empty.ReviewVersion));
        Assert.Throws<DomainException>(() => new TranslatedRecording(Guid.Empty, story.Recording));
        Assert.Throws<ArgumentNullException>(() => new TranslatedRecording(Guid.NewGuid(), null!));
    }
}
