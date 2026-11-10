namespace Mazwi.Domain.Tests;

public sealed class AiTranslationTests
{
    private static Story ApprovedStory()
    {
        Story story = Story.Upload(new StoryDetails("Poem", "Artist", "Shona", "ZW", "poem", "sn"), new StoryRecording("clip.wav", 12), true);
        story.SetTranscript("Original\nSecond line");
        story.SetTranslation("xh", "Translation", new TranslationNotes(["Keep Sekuru."]));
        story.SetTranslation("en", "English");
        foreach (string language in new[] { "sn", "xh", "en" })
        {
            story.Approve(language, story.ReviewVersion);
        }

        return story;
    }

    [Fact]
    public void TranslationNotes_ValidBounds_CopiesAndProtectsValues()
    {
        // Arrange
        string[] input = [" Note "];
        var notes = new TranslationNotes(input);
        // Act
        input[0] = "Changed";
        // Assert
        Assert.Equal("Note", Assert.Single(notes.Items));
        Assert.Throws<NotSupportedException>(() => ((IList<string>)notes.Items)[0] = "Changed");
        Assert.Empty(new TranslationNotes().Items);
        Assert.Equal(20, new TranslationNotes(Enumerable.Repeat(new string('a', 1000), 20)).Items.Count);
    }

    [Theory]
    [InlineData(21, "note")]
    [InlineData(1, " ")]
    [InlineData(1, "bad\0note")]
    public void TranslationNotes_InvalidValues_RejectsInput(int count, string note)
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => new TranslationNotes(Enumerable.Repeat(note, count)));
        Assert.Throws<DomainException>(() => new TranslationNotes([new string('x', 1001)]));
        Assert.Throws<DomainException>(() => new TranslationNotes([null!]));
    }

    [Fact]
    public void SetTranslation_NotesOnlyChange_RevokesTargetApprovalAndRetainsOtherLanguages()
    {
        // Arrange
        Story story = ApprovedStory();
        Guid version = story.ReviewVersion;
        // Act
        TranslationDraft draft = story.SetTranslation("xh", "Translation", new TranslationNotes(["Revised note"]));
        // Assert
        Assert.False(draft.IsApproved);
        Assert.True(story.OriginalApproved);
        Assert.True(story.Translations.Single(d => d.Language == "en").IsApproved);
        Assert.NotEqual(version, story.ReviewVersion);
        story.Approve("xh", story.ReviewVersion);
        Assert.True(story.SetTranslation("xh", "Translation").IsApproved);
        Assert.False(story.SetTranslation("xh", "Translation", new TranslationNotes()).IsApproved);
    }

    [Fact]
    public void Notes_RecordingApprovalAndSourceEdits_PreserveNotes()
    {
        // Arrange
        Story story = ApprovedStory();
        // Act
        story.AttachRecording("xh", new TranslatedRecording(Guid.NewGuid(), new StoryRecording("translated.wav", 12)), true);
        story.Approve("xh", story.ReviewVersion);
        Story loaded = Story.Rehydrate(story.Id, story.Details, story.Recording, story.PermissionRecordedAt,
            story.Transcript!.Value, story.Translations, story.ReviewVersion, story.OriginalApproved);
        loaded.SetTranscript("Changed original");
        // Assert
        Assert.False(loaded.OriginalApproved);
        Assert.False(loaded.Translations.Single(d => d.Language == "xh").IsApproved);
        Assert.Equal("Keep Sekuru.", Assert.Single(loaded.Translations.Single(d => d.Language == "xh").Notes.Items));
    }

    [Fact]
    public void SetAiTranslation_IdenticalResult_StillRequiresReview()
    {
        // Arrange
        Story story = ApprovedStory();
        // Act
        TranslationDraft draft = story.SetAiTranslation("xh", new(new StoryText("Translation"), new TranslationNotes(["Keep Sekuru."])), story.ReviewVersion);
        // Assert
        Assert.False(draft.IsApproved);
        Assert.True(story.OriginalApproved);
        Assert.True(story.Translations.Single(d => d.Language == "en").IsApproved);
    }

    [Theory]
    [InlineData("sn")]
    [InlineData("xx")]
    public void GetAiTranslationSource_InvalidTarget_RejectsDraft(string language)
    {
        // Arrange
        Story story = ApprovedStory();
        // Act / Assert
        Assert.Throws<AiDraftNotAllowedException>(() => story.GetAiTranslationSource(language, story.ReviewVersion));
    }

    [Fact]
    public void GetAiTranslationSource_UnapprovedOversizeAndStale_RejectsDraft()
    {
        // Arrange
        Story story = ApprovedStory();
        Guid old = story.ReviewVersion;
        story.SetTranscript(new string('a', 6000));
        // Act / Assert
        Assert.Throws<AiDraftNotAllowedException>(() => story.GetAiTranslationSource("xh", story.ReviewVersion));
        story.Approve("sn", story.ReviewVersion);
        Assert.Equal(6000, story.GetAiTranslationSource("xh", story.ReviewVersion).Length);
        Assert.Throws<StoryConflictException>(() => story.GetAiTranslationSource("xh", old));
        story.SetTranscript(new string('a', 6001)); story.Approve("sn", story.ReviewVersion);
        Assert.Throws<AiDraftNotAllowedException>(() => story.GetAiTranslationSource("xh", story.ReviewVersion));
    }
}
