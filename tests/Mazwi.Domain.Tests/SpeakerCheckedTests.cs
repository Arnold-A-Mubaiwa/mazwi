namespace Mazwi.Domain.Tests;

public sealed class SpeakerCheckedTests
{
    private static Story ApprovedOriginal()
    {
        Story story = Story.Upload(new StoryDetails("Story", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 19), true);
        story.SetTranscript("Kare kare"); story.SetTranslation("xh", "Kudala");
        story.Approve("sn", story.ReviewVersion);
        return story;
    }

    [Fact]
    public void Approve_TranslationWithoutSpeakerCheck_ApprovesAndRecordsNotChecked()
    {
        // Arrange
        Story story = ApprovedOriginal();
        // Act
        story.Approve("xh", story.ReviewVersion);
        // Assert
        TranslationDraft draft = story.Translations.Single();
        Assert.True(draft.IsApproved); Assert.False(draft.SpeakerChecked);
        ApprovedStoryVersion version = story.SelectApprovedVersion("xh")!;
        Assert.Equal("xh", version.TextLanguage); Assert.False(version.SpeakerChecked);
    }

    [Fact]
    public void Approve_TranslationWithSpeakerCheck_RecordsChecked()
    {
        // Arrange
        Story story = ApprovedOriginal();
        // Act
        story.Approve("xh", story.ReviewVersion, speakerChecked: true);
        // Assert
        Assert.True(story.Translations.Single().SpeakerChecked);
        Assert.True(story.SelectApprovedVersion("xh")!.SpeakerChecked);
    }

    [Fact]
    public void SelectApprovedVersion_Original_AlwaysCountsAsChecked()
    {
        // Arrange
        Story story = ApprovedOriginal();
        // Act
        ApprovedStoryVersion version = story.SelectApprovedVersion("sn")!;
        // Assert
        Assert.True(version.SpeakerChecked);
        Assert.True(story.SelectApprovedVersion("en")!.SpeakerChecked);
    }

    [Fact]
    public void SetTranslation_EditAfterCheckedApproval_ResetsSpeakerCheck()
    {
        // Arrange
        Story story = ApprovedOriginal();
        story.Approve("xh", story.ReviewVersion, speakerChecked: true);
        // Act
        story.SetTranslation("xh", "Kudala kakhulu");
        // Assert
        TranslationDraft draft = story.Translations.Single();
        Assert.False(draft.IsApproved); Assert.False(draft.SpeakerChecked);
    }

    [Fact]
    public void AttachGeneratedVoice_AfterCheckedApproval_ResetsSpeakerCheck()
    {
        // Arrange
        Story story = ApprovedOriginal();
        story.Approve("xh", story.ReviewVersion, speakerChecked: true);
        var voice = new TranslatedRecording(Guid.NewGuid(), new StoryRecording("voice.wav", 100), TranslatedRecording.StandardVoice);
        // Act
        story.AttachGeneratedVoice("xh", voice, story.ReviewVersion);
        // Assert
        Assert.False(story.Translations.Single().SpeakerChecked);
    }

    [Fact]
    public void Constructor_SpeakerCheckedWithoutApproval_Throws()
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => new TranslationDraft("xh", "Kudala", speakerChecked: true));
    }
}
