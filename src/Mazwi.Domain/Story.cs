namespace Mazwi.Domain;

public sealed class Story
{
    private readonly Dictionary<string, TranslationDraft> _translations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SubtitleTiming> _timings = new(StringComparer.Ordinal);
    public Guid Id { get; }
    public StoryDetails Details { get; }
    public StoryRecording Recording { get; }
    public DateTimeOffset PermissionRecordedAt { get; }
    // Null only for stories saved before accounts existed; nobody can change them until the owner claims them.
    public Guid? OwnerId { get; }
    public string Status => OriginalApproved ? "published" : "unpublished";
    public Guid ReviewVersion { get; private set; } = Guid.NewGuid();
    public bool OriginalApproved { get; private set; }
    public StoryText? Transcript { get; private set; }
    public IReadOnlyCollection<TranslationDraft> Translations => _translations.Values.ToArray();
    public IReadOnlyDictionary<string, SubtitleTiming> SubtitleTimings => new Dictionary<string, SubtitleTiming>(_timings, StringComparer.Ordinal);

    private Story(Guid id, StoryDetails details, StoryRecording recording, DateTimeOffset permissionRecordedAt, Guid? ownerId)
    {
        if (id == Guid.Empty || permissionRecordedAt == default)
        {
            throw new DomainException("A story requires an identity and recorded permission.");
        }

        if (ownerId == Guid.Empty)
        {
            throw new DomainException("A story owner requires an identity.");
        }

        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(recording);
        Id = id;
        Details = details;
        Recording = recording;
        PermissionRecordedAt = permissionRecordedAt;
        OwnerId = ownerId;
    }

    public static Story Upload(StoryDetails details, StoryRecording recording, bool permission, Guid ownerId)
    {
        if (!permission)
        {
            throw new DomainException("Permission to store and process this recording is required.");
        }

        return new Story(Guid.NewGuid(), details, recording, DateTimeOffset.UtcNow, ownerId);
    }

    public bool IsOwnedBy(Guid artistId) => artistId != Guid.Empty && OwnerId == artistId;

    public void EnsureOwnedBy(Guid artistId)
    {
        if (!IsOwnedBy(artistId))
        {
            throw new StoryAccessDeniedException();
        }
    }

    public void SetTranscript(string text)
    {
        var value = new StoryText(text);
        if (value == Transcript)
        {
            return;
        }

        Transcript = value;
        _timings.Remove(Details.Language);
        OriginalApproved = false;
        foreach (TranslationDraft draft in Translations)
        {
            _translations[draft.Language] = new TranslationDraft(draft.Language, draft.Text.Value, draft.Recording, notes: draft.Notes);
        }

        ReviewVersion = Guid.NewGuid();
    }

    // An AI transcript is a normal source edit: it revokes every approval. The version check stops it overwriting newer edits.
    public void SetAiTranscript(StoryText transcript, Guid expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        EnsureVersion(expectedVersion);
        SetTranscript(transcript.Value);
    }

    public TranslationDraft SetTranslation(string language, string text, TranslationNotes? notes = null, bool requireReview = false)
    {
        if (Transcript is null)
        {
            throw new DomainException("Save the original transcript before adding a translation.");
        }

        var draft = new TranslationDraft(language, text);
        if (draft.Language == Details.Language)
        {
            throw new DomainException("Choose a translation language different from the original.");
        }

        _translations.TryGetValue(draft.Language, out TranslationDraft? previous);
        notes ??= previous?.Notes ?? new TranslationNotes();
        if (!requireReview && previous?.Text == draft.Text && previous.Notes.Items.SequenceEqual(notes.Items))
        {
            return previous;
        }

        if (previous?.Text != draft.Text)
        {
            _timings.Remove(draft.Language);
        }

        draft = new TranslationDraft(draft.Language, draft.Text.Value, previous?.Recording, notes: notes);
        _translations[draft.Language] = draft;
        ReviewVersion = Guid.NewGuid();
        return draft;
    }

    public void AttachRecording(string language, TranslatedRecording recording, bool permission)
    {
        if (!permission)
        {
            throw new DomainException("Permission to use this translated recording is required.");
        }

        ArgumentNullException.ThrowIfNull(recording);
        if (recording.Voice is not null)
        {
            throw new DomainException("Uploaded recordings cannot be marked as AI voices.");
        }

        if (!_translations.TryGetValue(language, out TranslationDraft? draft))
        {
            throw new DomainException("Save the translation text before adding its recording.");
        }

        ReplaceRecording(draft, recording);
    }

    public SpeechScript GetSpeechScript(string language, Guid expectedVersion)
    {
        EnsureVersion(expectedVersion);
        if (!_translations.TryGetValue(language, out TranslationDraft? draft))
        {
            throw new AiDraftNotAllowedException("Save translation text before generating a voice.");
        }

        return new SpeechScript(draft.Text.Value);
    }

    public void AttachGeneratedVoice(string language, TranslatedRecording recording, Guid expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(recording);
        GetSpeechScript(language, expectedVersion);
        if (recording.Voice is null)
        {
            throw new DomainException("A generated voice must record its AI voice kind.");
        }

        ReplaceRecording(_translations[language], recording);
    }

    // Any new recording, uploaded or generated, needs the artist to approve the language again.
    private void ReplaceRecording(TranslationDraft draft, TranslatedRecording recording)
    {
        _translations[draft.Language] = new TranslationDraft(draft.Language, draft.Text.Value, recording, notes: draft.Notes);
        _timings.Remove(draft.Language);
        ReviewVersion = Guid.NewGuid();
    }

    // speakerChecked records whether the artist speaks the translation language and checked it; the original always counts as checked.
    public void Approve(string language, Guid expectedVersion, bool speakerChecked = false)
    {
        EnsureVersion(expectedVersion);
        if (Transcript is null)
        {
            throw new DomainException("Save the original transcript before approval.");
        }

        if (language == Details.Language)
        {
            OriginalApproved = true;
        }
        else
        {
            if (!OriginalApproved)
            {
                throw new DomainException("Approve the original before approving translations.");
            }

            if (!_translations.TryGetValue(language, out TranslationDraft? draft))
            {
                throw new DomainException("Save this translation before approval.");
            }

            _translations[language] = new TranslationDraft(language, draft.Text.Value, draft.Recording, true, draft.Notes, speakerChecked);
        }
    }

    public void EnsureVersion(Guid expectedVersion)
    {
        if (expectedVersion != ReviewVersion)
        {
            throw new StoryConflictException();
        }
    }

    public string GetAiTranslationSource(string language, Guid expectedVersion)
    {
        EnsureVersion(expectedVersion);
        if (Transcript is null || !OriginalApproved)
        {
            throw new AiDraftNotAllowedException("Save and approve the original transcript before requesting an AI draft.");
        }

        if (!StoryDetails.SupportsLanguage(language) || language == Details.Language)
        {
            throw new AiDraftNotAllowedException("Choose a supported language different from the original.");
        }

        if (Transcript.Value.Length > 6000)
        {
            throw new AiDraftNotAllowedException("AI drafts support up to 6,000 source characters. Use manual translation for longer stories.");
        }

        return Transcript.Value;
    }

    public TranslationDraft SetAiTranslation(string language, TranslationDraftResult result, Guid expectedVersion)
    {
        GetAiTranslationSource(language, expectedVersion);
        return SetTranslation(language, result.Translation.Value, result.Notes, requireReview: true);
    }

    public ApprovedStoryVersion? SelectApprovedVersion(string language, bool originalAudio = false)
    {
        if (!StoryDetails.SupportsLanguage(language))
        {
            throw new DomainException("Choose a supported viewing language.");
        }

        if (!OriginalApproved || Transcript is null)
        {
            return null;
        }

        TranslationDraft? translation = Translations.SingleOrDefault(d => d.Language == language && d.IsApproved);
        if (translation is null)
        {
            return new(Details.Language, Details.Language, Transcript, Id, Recording, Cues: CuesFor(Details.Language, Transcript));
        }

        if (!originalAudio && translation.Recording is TranslatedRecording media)
        {
            return new(translation.Language, translation.Language, translation.Text, media.Id, media.File, media.Voice, translation.SpeakerChecked,
                CuesFor(translation.Language, translation.Text));
        }

        // A translation's timing follows the audio the artist timed it against: its own recording if it has one, otherwise the original.
        IReadOnlyList<SubtitleCue>? cues = translation.Recording is null ? CuesFor(translation.Language, translation.Text) : null;
        return new(translation.Language, Details.Language, translation.Text, Id, Recording, SpeakerChecked: translation.SpeakerChecked, Cues: cues);
    }

    // A play counts only for the approved version the viewer was shown; it is credited to the language they read.
    // Returns null for an unapproved story, an unsupported language or a version that has since changed.
    public StoryPlay? RecordPlay(string language, Guid version, DateOnly day)
    {
        if (!StoryDetails.SupportsLanguage(language) || version != ReviewVersion)
        {
            return null;
        }

        ApprovedStoryVersion? approved = SelectApprovedVersion(language);
        return approved is null ? null : new StoryPlay(Id, approved.TextLanguage, day);
    }

    private IReadOnlyList<SubtitleCue>? CuesFor(string language, StoryText text) =>
        _timings.TryGetValue(language, out SubtitleTiming? timing) ? timing.Cues(text) : null;

    // Timing is part of what the artist approves: saving it removes that language's approval, and for the original every approval.
    public void SetSubtitleTiming(string language, SubtitleTiming timing, Guid expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(timing);
        EnsureVersion(expectedVersion);
        StoryText text = TextFor(language) ?? throw new DomainException("Save this language's text before timing its subtitles.");
        if (!timing.Fits(text))
        {
            throw new DomainException($"Time every line: the text has {SubtitleTiming.Lines(text).Count} lines but {timing.StartsMs.Count} times were sent.");
        }

        _timings[language] = timing;
        if (language == Details.Language)
        {
            OriginalApproved = false;
            foreach (TranslationDraft draft in Translations)
            {
                _translations[draft.Language] = new TranslationDraft(draft.Language, draft.Text.Value, draft.Recording, notes: draft.Notes);
            }
        }
        else
        {
            TranslationDraft draft = _translations[language];
            _translations[language] = new TranslationDraft(language, draft.Text.Value, draft.Recording, notes: draft.Notes);
        }

        ReviewVersion = Guid.NewGuid();
    }

    private StoryText? TextFor(string language) =>
        language == Details.Language ? Transcript : _translations.TryGetValue(language, out TranslationDraft? draft) ? draft.Text : null;

    public static Story Rehydrate(Guid id, StoryDetails details, StoryRecording recording, DateTimeOffset permissionRecordedAt,
        string? transcript = null, IEnumerable<TranslationDraft>? translations = null, Guid? reviewVersion = null, bool originalApproved = false,
        Guid? ownerId = null, IEnumerable<KeyValuePair<string, SubtitleTiming>>? timings = null)
    {
        var story = new Story(id, details, recording, permissionRecordedAt, ownerId);
        if (transcript is not null)
        {
            story.SetTranscript(transcript);
        }

        foreach (TranslationDraft draft in translations ?? [])
        {
            story.SetTranslation(draft.Language, draft.Text.Value);
            story._translations[draft.Language] = draft;
        }

        if (originalApproved && story.Transcript is null)
        {
            throw new DomainException("Approval requires a transcript.");
        }

        if (!originalApproved && story.Translations.Any(draft => draft.IsApproved))
        {
            throw new DomainException("Translations require an approved original.");
        }

        foreach ((string language, SubtitleTiming timing) in timings ?? [])
        {
            StoryText text = story.TextFor(language) ?? throw new DomainException("Subtitle timing requires saved text.");
            if (!timing.Fits(text))
            {
                throw new DomainException("Subtitle timing does not match its text.");
            }

            story._timings[language] = timing;
        }

        story.OriginalApproved = originalApproved;
        story.ReviewVersion = reviewVersion ?? Guid.NewGuid();
        if (story.ReviewVersion == Guid.Empty)
        {
            throw new DomainException("A review version is required.");
        }

        return story;
    }
}
