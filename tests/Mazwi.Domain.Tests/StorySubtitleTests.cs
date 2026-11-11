namespace Mazwi.Domain.Tests;

public sealed class StorySubtitleTests
{
    private static readonly SubtitleTiming TwoCues = new([0, 1000], 3000);

    private static Story ApprovedStory(bool translatedRecording = false)
    {
        Story story = Story.Upload(new StoryDetails("Story", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 12), true, TestOwner.Id);
        story.SetTranscript("Kare kare\nPane imwe nguva");
        story.SetTranslation("xh", "Kudala\nKwakukho");
        if (translatedRecording)
        {
            story.AttachRecording("xh", new TranslatedRecording(Guid.NewGuid(), new StoryRecording("xh.wav", 12)), true);
        }

        story.Approve("sn", story.ReviewVersion);
        story.Approve("xh", story.ReviewVersion);
        return story;
    }

    [Fact]
    public void SetSubtitleTiming_Original_RevokesEveryApprovalAndChangesVersion()
    {
        // Arrange
        Story story = ApprovedStory();
        Guid version = story.ReviewVersion;
        // Act
        story.SetSubtitleTiming("sn", TwoCues, version);
        // Assert
        Assert.Equal(TwoCues, story.SubtitleTimings["sn"]);
        Assert.False(story.OriginalApproved);
        Assert.All(story.Translations, draft => Assert.False(draft.IsApproved));
        Assert.NotEqual(version, story.ReviewVersion);
    }

    [Fact]
    public void SetSubtitleTiming_Translation_RevokesOnlyThatLanguage()
    {
        // Arrange
        Story story = ApprovedStory();
        // Act
        story.SetSubtitleTiming("xh", TwoCues, story.ReviewVersion);
        // Assert
        Assert.True(story.OriginalApproved);
        Assert.False(story.Translations.Single().IsApproved);
    }

    [Fact]
    public void SetSubtitleTiming_StaleVersion_ThrowsConflictWithoutSaving()
    {
        // Arrange
        Story story = ApprovedStory();
        // Act / Assert
        Assert.Throws<StoryConflictException>(() => story.SetSubtitleTiming("sn", TwoCues, Guid.NewGuid()));
        Assert.Empty(story.SubtitleTimings);
    }

    [Fact]
    public void SetSubtitleTiming_NoTextOrWrongLineCount_Throws()
    {
        // Arrange
        Story story = ApprovedStory();
        Story empty = Story.Upload(story.Details, story.Recording, true, TestOwner.Id);
        // Act / Assert
        Assert.Throws<DomainException>(() => story.SetSubtitleTiming("en", TwoCues, story.ReviewVersion));
        Assert.Throws<DomainException>(() => empty.SetSubtitleTiming("sn", TwoCues, empty.ReviewVersion));
        Assert.Throws<DomainException>(() => story.SetSubtitleTiming("sn", new SubtitleTiming([0], 900), story.ReviewVersion));
        Assert.Throws<ArgumentNullException>(() => story.SetSubtitleTiming("sn", null!, story.ReviewVersion));
        Assert.True(story.OriginalApproved);
    }

    [Fact]
    public void TextAndRecordingChanges_RemoveOnlyTheAffectedTiming()
    {
        // Arrange
        Story story = ApprovedStory();
        story.SetSubtitleTiming("sn", TwoCues, story.ReviewVersion);
        story.SetSubtitleTiming("xh", TwoCues, story.ReviewVersion);
        // Act
        story.SetTranslation("xh", "Kudala\nKwakukho", new TranslationNotes(["A note"]));
        bool keptAfterNotes = story.SubtitleTimings.ContainsKey("xh");
        story.SetTranslation("xh", "Kudala kakhulu\nKwakukho");
        bool keptAfterText = story.SubtitleTimings.ContainsKey("xh");
        story.SetSubtitleTiming("xh", TwoCues, story.ReviewVersion);
        story.AttachRecording("xh", new TranslatedRecording(Guid.NewGuid(), new StoryRecording("xh.wav", 12)), true);
        bool keptAfterRecording = story.SubtitleTimings.ContainsKey("xh");
        story.SetTranscript("Kare kare\nPane imwe nguva yakare");
        // Assert
        Assert.True(keptAfterNotes);
        Assert.False(keptAfterText);
        Assert.False(keptAfterRecording);
        Assert.Empty(story.SubtitleTimings);
    }

    [Fact]
    public void SelectApprovedVersion_TimedOriginal_ReturnsCues()
    {
        // Arrange
        Story story = ApprovedStory();
        story.SetSubtitleTiming("sn", TwoCues, story.ReviewVersion);
        story.Approve("sn", story.ReviewVersion);
        // Act
        ApprovedStoryVersion version = story.SelectApprovedVersion("sn")!;
        // Assert
        Assert.Equal([new SubtitleCue(0, 1000, "Kare kare"), new SubtitleCue(1000, 3000, "Pane imwe nguva")], version.Cues);
    }

    [Fact]
    public void SelectApprovedVersion_TranslationWithoutRecording_UsesItsTimingWithOriginalAudio()
    {
        // Arrange
        Story story = ApprovedStory();
        story.SetSubtitleTiming("xh", TwoCues, story.ReviewVersion);
        story.Approve("xh", story.ReviewVersion);
        // Act
        ApprovedStoryVersion version = story.SelectApprovedVersion("xh")!;
        // Assert
        Assert.Equal("sn", version.AudioLanguage);
        Assert.Equal("Kudala", version.Cues![0].Text);
    }

    [Fact]
    public void SelectApprovedVersion_TranslationWithRecording_HasCuesOnlyWithItsOwnAudio()
    {
        // Arrange
        Story story = ApprovedStory(translatedRecording: true);
        story.SetSubtitleTiming("xh", TwoCues, story.ReviewVersion);
        story.Approve("xh", story.ReviewVersion);
        // Act
        ApprovedStoryVersion own = story.SelectApprovedVersion("xh")!;
        ApprovedStoryVersion original = story.SelectApprovedVersion("xh", originalAudio: true)!;
        // Assert
        Assert.Equal("xh", own.AudioLanguage);
        Assert.NotNull(own.Cues);
        Assert.Null(original.Cues);
    }

    [Fact]
    public void SelectApprovedVersion_UntimedLanguage_HasNoCues()
    {
        // Act / Assert
        Assert.Null(ApprovedStory().SelectApprovedVersion("sn")!.Cues);
    }

    [Fact]
    public void Rehydrate_Timings_KeepsMatchingAndRejectsMismatchedOrOrphaned()
    {
        // Arrange
        var details = new StoryDetails("Story", "Artist", "Shona", "ZW", "story", "sn");
        var recording = new StoryRecording("clip.wav", 12);
        // Act
        Story story = Story.Rehydrate(Guid.NewGuid(), details, recording, DateTimeOffset.UtcNow, "Kare kare\nPane imwe nguva",
            timings: [KeyValuePair.Create("sn", TwoCues)]);
        // Assert
        Assert.Equal(TwoCues, story.SubtitleTimings["sn"]);
        Assert.Throws<DomainException>(() => Story.Rehydrate(Guid.NewGuid(), details, recording, DateTimeOffset.UtcNow, "One line",
            timings: [KeyValuePair.Create("sn", TwoCues)]));
        Assert.Throws<DomainException>(() => Story.Rehydrate(Guid.NewGuid(), details, recording, DateTimeOffset.UtcNow, "Kare kare\nPane imwe nguva",
            timings: [KeyValuePair.Create("xh", TwoCues)]));
    }
}
