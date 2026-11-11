namespace Mazwi.Domain.Tests;

public sealed class StoryLanguageTests
{
    [Fact]
    public void All_Catalogue_HasTwentyFiveUniqueTwoLetterCodesWithNamesAndGreetings()
    {
        // Arrange / Act
        IReadOnlyList<StoryLanguage> languages = StoryLanguages.All;
        // Assert
        Assert.Equal(25, languages.Count);
        Assert.Equal(languages.Count, languages.Select(language => language.Code).Distinct().Count());
        Assert.All(languages, language =>
        {
            Assert.Matches("^[a-z]{2}$", language.Code);
            Assert.False(string.IsNullOrWhiteSpace(language.Name));
            Assert.False(string.IsNullOrWhiteSpace(language.NativeName));
            Assert.False(string.IsNullOrWhiteSpace(language.EnglishName));
            Assert.False(string.IsNullOrWhiteSpace(language.Greeting));
        });
    }

    [Fact]
    public void All_Catalogue_OnlyPilotLanguagesAreAiChecked()
    {
        // Act
        string[] checkedCodes = StoryLanguages.All.Where(language => language.AiChecked).Select(language => language.Code).ToArray();
        // Assert
        Assert.Equal(["sn", "xh", "sw", "en"], checkedCodes);
        Assert.Equal("sn", StoryLanguages.Default);
        Assert.True(StoryLanguages.IsSupported(StoryLanguages.Default));
    }

    [Theory]
    [InlineData("sn")]
    [InlineData("nd")]
    [InlineData("yo")]
    [InlineData("am")]
    [InlineData("ar")]
    [InlineData("pt")]
    public void IsSupported_CatalogueCode_ReturnsTrue(string code)
    {
        // Act / Assert
        Assert.True(StoryLanguages.IsSupported(code));
        Assert.Equal(code, StoryLanguages.Get(code).Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("xx")]
    [InlineData("SN")]
    [InlineData(" sn")]
    [InlineData("eng")]
    [InlineData("de")]
    public void IsSupported_UnknownOrMalformedCode_ReturnsFalse(string? code)
    {
        // Act / Assert
        Assert.False(StoryLanguages.IsSupported(code));
        Assert.Null(StoryLanguages.Find(code));
        Assert.Equal("Choose one of the languages Mazwi supports.", Assert.Throws<DomainException>(() => StoryLanguages.Get(code)).Message);
    }

    [Fact]
    public void StoryDetails_NewCatalogueLanguage_IsAcceptedForUploadTranslationAndPlays()
    {
        // Arrange
        Story story = Story.Upload(new StoryDetails("Tsuro na Gudo", "Artist", "Yoruba", "NG", "folktale", "yo"), new StoryRecording("clip.wav", 12), true, TestOwner.Id);
        story.SetTranscript("Ní ayé àtijọ́");
        // Act
        TranslationDraft draft = story.SetTranslation("ha", "A da");
        var play = new StoryPlay(story.Id, "wo", new DateOnly(2026, 11, 11));
        // Assert
        Assert.Equal("yo", story.Details.Language);
        Assert.Equal("ha", draft.Language);
        Assert.Equal("wo", play.Language);
        Assert.Throws<DomainException>(() => new StoryDetails("Story", "Artist", "Shona", "ZW", "story", "de"));
    }
}
