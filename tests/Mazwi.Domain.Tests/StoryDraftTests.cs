namespace Mazwi.Domain.Tests;

public sealed class StoryDraftTests
{
    private static Story NewStory() => Story.Upload(new StoryDetails("Tsuro", "Rudo", "Shona", "ZW", "folktale", "sn"), new StoryRecording("clip.wav", 12), true, TestOwner.Id);

    [Fact]
    public void SetTranscript_ValidText_PreservesInternalFormattingAndUnpublishedState()
    {
        // Arrange
        Story story = NewStory();
        // Act
        story.SetTranscript("  Tsuro na Gudo\n\nSekuru\tRudo.  ");
        // Assert
        Assert.Equal("Tsuro na Gudo\n\nSekuru\tRudo.", story.Transcript?.Value);
        Assert.Equal("unpublished", story.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \r\n\t ")]
    [InlineData("text\0text")]
    [InlineData("text\u0001text")]
    public void Constructor_InvalidText_RejectsInput(string? text)
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => new StoryText(text!));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20000)]
    public void Constructor_BoundaryTextLength_AcceptsText(int length)
    {
        // Act / Assert
        Assert.Equal(length, new StoryText(new string('a', length)).Value.Length);
    }

    [Fact]
    public void SetTranscript_OverlongText_DoesNotReplaceExistingText()
    {
        // Arrange
        Story story = NewStory();
        story.SetTranscript("Original");
        // Act / Assert
        Assert.Throws<DomainException>(() => story.SetTranscript(new string('a', 20001)));
        Assert.Equal("Original", story.Transcript?.Value);
    }

    [Theory]
    [InlineData("xh")]
    [InlineData("sw")]
    [InlineData("en")]
    public void SetTranslation_ValidTarget_AddsDraft(string language)
    {
        // Arrange
        Story story = NewStory();
        story.SetTranscript("Original");
        // Act
        TranslationDraft draft = story.SetTranslation(language, "  A translated line\nAnother line. ");
        // Assert
        Assert.Equal("A translated line\nAnother line.", draft.Text.Value);
        Assert.Equal("draft", draft.Status);
        Assert.Equal(draft, Assert.Single(story.Translations));
        Assert.Equal("unpublished", story.Status);
    }

    [Fact]
    public void SetTranslation_NoTranscript_RejectsTranslation()
    {
        // Arrange
        Story story = NewStory();
        // Act / Assert
        Assert.Throws<DomainException>(() => story.SetTranslation("xh", "Translation"));
        Assert.Empty(story.Translations);
    }

    [Theory]
    [InlineData("sn", "Translation")]
    [InlineData("xx", "Translation")]
    [InlineData("", "Translation")]
    [InlineData("xh", " ")]
    [InlineData("xh", "text\0")]
    public void SetTranslation_InvalidTargetOrText_LeavesStateUnchanged(string language, string text)
    {
        // Arrange
        Story story = NewStory();
        story.SetTranscript("Original");
        // Act / Assert
        Assert.Throws<DomainException>(() => story.SetTranslation(language, text));
        Assert.Empty(story.Translations);
    }

    [Fact]
    public void SetTranslation_RepeatedTarget_ReplacesOnlyThatDraft()
    {
        // Arrange
        Story story = NewStory();
        story.SetTranscript("Original");
        story.SetTranslation("xh", "First");
        story.SetTranslation("en", "English draft");
        // Act
        story.SetTranslation("xh", "Replacement");
        story.SetTranscript("Revised original");
        // Assert
        Assert.Equal(2, story.Translations.Count);
        Assert.Equal("Replacement", story.Translations.Single(d => d.Language == "xh").Text.Value);
        Assert.Equal("English draft", story.Translations.Single(d => d.Language == "en").Text.Value);
        Assert.All(story.Translations, draft => Assert.Equal("draft", draft.Status));
    }

    [Fact]
    public void Rehydrate_SavedTextAndTranslations_RestoresValidatedDraftState()
    {
        // Arrange
        Story original = NewStory();
        original.SetTranscript("Original");
        original.SetTranslation("xh", "Translation");
        // Act
        Story loaded = Story.Rehydrate(original.Id, original.Details, original.Recording, original.PermissionRecordedAt,
            original.Transcript?.Value, original.Translations);
        // Assert
        Assert.Equal(original.Transcript, loaded.Transcript);
        Assert.Equal(original.Translations, loaded.Translations);
        Assert.Throws<DomainException>(() => Story.Rehydrate(original.Id, original.Details, original.Recording,
            original.PermissionRecordedAt, null, original.Translations));
    }

    [Fact]
    public void Translations_ModifiedCollectionSnapshot_DoesNotMutateAggregate()
    {
        // Arrange
        Story story = NewStory();
        story.SetTranscript("Original");
        story.SetTranslation("xh", "Original draft");
        // Act
        var snapshot = Assert.IsType<TranslationDraft[]>(story.Translations);
        snapshot[0] = new TranslationDraft("en", "Different draft");
        // Assert
        Assert.Equal("xh", Assert.Single(story.Translations).Language);
    }
}
