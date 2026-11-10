namespace Mazwi.Domain.Tests;

public sealed class AiVoiceTests
{
    private static Story ApprovedStory()
    {
        Story story = Story.Upload(new StoryDetails("Story", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 12), true);
        story.SetTranscript("Original");
        story.SetTranslation("xh", "Molo. Unjani?");
        story.SetTranslation("en", "Hello");
        story.Approve("sn", story.ReviewVersion);
        story.Approve("xh", story.ReviewVersion);
        story.Approve("en", story.ReviewVersion);
        return story;
    }

    private static TranslatedRecording Voice(string? kind = TranslatedRecording.StandardVoice) =>
        new(Guid.NewGuid(), new StoryRecording("voice.wav", 100), kind);

    [Fact]
    public void SpeechScript_ShortText_KeepsOnePartWithLineBreaks()
    {
        // Act
        var script = new SpeechScript("  Line one\nLine two  ");
        // Assert
        Assert.Equal("Line one\nLine two", Assert.Single(script.Parts));
    }

    [Fact]
    public void SpeechScript_LongText_SplitsAtSentenceEndsWithinPartLimit()
    {
        // Arrange
        string sentence = new string('a', 298) + ". ";
        // Act
        var script = new SpeechScript(string.Concat(Enumerable.Repeat(sentence, 5)));
        // Assert
        Assert.Equal(3, script.Parts.Count);
        Assert.All(script.Parts, part => Assert.True(part.Length <= SpeechScript.MaximumPartCharacters));
        Assert.All(script.Parts, part => Assert.EndsWith(".", part));
    }

    [Fact]
    public void SpeechScript_SentenceLongerThanPart_CutsAtWordBoundary()
    {
        // Arrange
        string words = string.Join(' ', Enumerable.Repeat("word", 200));
        // Act
        var script = new SpeechScript(words);
        // Assert
        Assert.True(script.Parts.Count > 1);
        Assert.All(script.Parts, part => Assert.True(part.Length <= SpeechScript.MaximumPartCharacters));
        Assert.Equal(words, string.Join(' ', script.Parts));
    }

    [Fact]
    public void SpeechScript_UnbrokenText_HardCutsAtPartLimit()
    {
        // Act
        var script = new SpeechScript(new string('x', 1_000));
        // Assert
        Assert.Equal([600, 400], script.Parts.Select(part => part.Length));
    }

    [Fact]
    public void SpeechScript_ExactlyMaximumCharacters_IsAllowed()
    {
        // Act
        var script = new SpeechScript(new string('x', SpeechScript.MaximumCharacters));
        // Assert
        Assert.Equal(SpeechScript.MaximumCharacters, script.Parts.Sum(part => part.Length));
    }

    [Theory]
    [InlineData(SpeechScript.MaximumCharacters + 1)]
    [InlineData(0)]
    public void SpeechScript_OversizedOrEmpty_ThrowsNotAllowed(int length)
    {
        // Act / Assert
        Assert.Throws<AiDraftNotAllowedException>(() => new SpeechScript(new string('x', length)));
    }

    [Fact]
    public void SpeechScript_Whitespace_ThrowsNotAllowed()
    {
        // Act / Assert
        Assert.Throws<AiDraftNotAllowedException>(() => new SpeechScript(" \n "));
    }

    [Fact]
    public void GeneratedSpeech_HeaderOnlyOrOverTwoMinutes_ThrowsDomainException()
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => new GeneratedSpeech(new byte[SpeechScript.WavHeaderBytes]));
        Assert.Throws<DomainException>(() => new GeneratedSpeech(new byte[SpeechScript.WavHeaderBytes + SpeechScript.MaximumAudioBytes + 1]));
    }

    [Fact]
    public void GeneratedSpeech_CopiesInput_IsImmutable()
    {
        // Arrange
        byte[] wav = new byte[SpeechScript.WavHeaderBytes + 2];
        var speech = new GeneratedSpeech(wav);
        wav[0] = 9;
        // Act
        using Stream content = speech.OpenRead();
        // Assert
        Assert.Equal(0, content.ReadByte());
        Assert.False(content.CanWrite);
        Assert.Equal(SpeechScript.WavHeaderBytes + 2, speech.Length);
    }

    [Fact]
    public void TranslatedRecording_UnknownVoice_ThrowsDomainException()
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => Voice("robot"));
        Assert.Null(Voice(null).Voice);
        Assert.Equal("clone", Voice(TranslatedRecording.CloneVoice).Voice);
    }

    [Fact]
    public void GetSpeechScript_SavedTranslation_ReturnsSplitText()
    {
        // Arrange
        Story story = ApprovedStory();
        // Act
        SpeechScript script = story.GetSpeechScript("xh", story.ReviewVersion);
        // Assert
        Assert.Equal("Molo. Unjani?", Assert.Single(script.Parts));
    }

    [Fact]
    public void GetSpeechScript_NoSavedTranslation_ThrowsNotAllowed()
    {
        // Arrange
        Story story = Story.Upload(new StoryDetails("Story", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 12), true);
        // Act / Assert
        Assert.Throws<AiDraftNotAllowedException>(() => story.GetSpeechScript("xh", story.ReviewVersion));
    }

    [Fact]
    public void GetSpeechScript_StaleVersion_ThrowsConflict()
    {
        // Arrange
        Story story = ApprovedStory();
        // Act / Assert
        Assert.Throws<StoryConflictException>(() => story.GetSpeechScript("xh", Guid.NewGuid()));
    }

    [Fact]
    public void AttachGeneratedVoice_ApprovedLanguage_RevokesOnlyThatLanguageAndRecordsVoice()
    {
        // Arrange
        Story story = ApprovedStory();
        Guid version = story.ReviewVersion;
        // Act
        story.AttachGeneratedVoice("xh", Voice(), version);
        // Assert
        TranslationDraft xhosa = story.Translations.Single(d => d.Language == "xh");
        Assert.False(xhosa.IsApproved);
        Assert.Equal(TranslatedRecording.StandardVoice, xhosa.Recording!.Voice);
        Assert.True(story.Translations.Single(d => d.Language == "en").IsApproved);
        Assert.True(story.OriginalApproved);
        Assert.NotEqual(version, story.ReviewVersion);
    }

    [Fact]
    public void AttachGeneratedVoice_UploadedRecording_ThrowsDomainException()
    {
        // Arrange
        Story story = ApprovedStory();
        // Act / Assert
        Assert.Throws<DomainException>(() => story.AttachGeneratedVoice("xh", Voice(null), story.ReviewVersion));
    }

    [Fact]
    public void AttachRecording_RecordingMarkedAsAiVoice_ThrowsDomainException()
    {
        // Arrange
        Story story = ApprovedStory();
        // Act / Assert
        Assert.Throws<DomainException>(() => story.AttachRecording("xh", Voice(), true));
    }

    [Fact]
    public void SelectApprovedVersion_ApprovedAiVoice_ExposesVoiceKind()
    {
        // Arrange
        Story story = ApprovedStory();
        story.AttachGeneratedVoice("xh", Voice(), story.ReviewVersion);
        story.Approve("xh", story.ReviewVersion);
        // Act
        ApprovedStoryVersion? version = story.SelectApprovedVersion("xh");
        // Assert
        Assert.Equal(TranslatedRecording.StandardVoice, version!.Voice);
        Assert.Equal("xh", version.AudioLanguage);
    }
}
