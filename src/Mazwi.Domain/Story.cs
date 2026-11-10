namespace Mazwi.Domain;

public sealed class Story
{
    private readonly Dictionary<string, TranslationDraft> _translations = new(StringComparer.Ordinal);
    public Guid Id { get; }
    public StoryDetails Details { get; }
    public StoryRecording Recording { get; }
    public DateTimeOffset PermissionRecordedAt { get; }
    public string Status => OriginalApproved ? "published" : "unpublished";
    public Guid ReviewVersion { get; private set; } = Guid.NewGuid();
    public bool OriginalApproved { get; private set; }
    public StoryText? Transcript { get; private set; }
    public IReadOnlyCollection<TranslationDraft> Translations => _translations.Values.ToArray();

    private Story(Guid id, StoryDetails details, StoryRecording recording, DateTimeOffset permissionRecordedAt)
    {
        if (id == Guid.Empty || permissionRecordedAt == default)
        {
            throw new DomainException("A story requires an identity and recorded permission.");
        }

        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(recording);
        Id = id;
        Details = details;
        Recording = recording;
        PermissionRecordedAt = permissionRecordedAt;
    }

    public static Story Upload(StoryDetails details, StoryRecording recording, bool permission)
    {
        if (!permission)
        {
            throw new DomainException("Permission to store and process this recording is required.");
        }

        return new Story(Guid.NewGuid(), details, recording, DateTimeOffset.UtcNow);
    }

    public void SetTranscript(string text)
    {
        var value = new StoryText(text);
        if (value == Transcript)
        {
            return;
        }

        Transcript = value;
        OriginalApproved = false;
        foreach (TranslationDraft draft in Translations)
        {
            _translations[draft.Language] = new TranslationDraft(draft.Language, draft.Text.Value, draft.Recording);
        }

        ReviewVersion = Guid.NewGuid();
    }

    public TranslationDraft SetTranslation(string language, string text)
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
        if (previous?.Text == draft.Text)
        {
            return previous;
        }

        draft = new TranslationDraft(draft.Language, draft.Text.Value, previous?.Recording);
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
        if (!_translations.TryGetValue(language, out TranslationDraft? draft))
        {
            throw new DomainException("Save the translation text before adding its recording.");
        }

        _translations[language] = new TranslationDraft(language, draft.Text.Value, recording);
        ReviewVersion = Guid.NewGuid();
    }

    public void Approve(string language, Guid expectedVersion)
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

            _translations[language] = new TranslationDraft(language, draft.Text.Value, draft.Recording, true);
        }
    }

    public void EnsureVersion(Guid expectedVersion)
    {
        if (expectedVersion != ReviewVersion)
        {
            throw new StoryConflictException();
        }
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
            return new(Details.Language, Details.Language, Transcript, Id, Recording);
        }

        if (!originalAudio && translation.Recording is TranslatedRecording media)
        {
            return new(translation.Language, translation.Language, translation.Text, media.Id, media.File);
        }

        return new(translation.Language, Details.Language, translation.Text, Id, Recording);
    }

    public static Story Rehydrate(Guid id, StoryDetails details, StoryRecording recording, DateTimeOffset permissionRecordedAt,
        string? transcript = null, IEnumerable<TranslationDraft>? translations = null, Guid? reviewVersion = null, bool originalApproved = false)
    {
        var story = new Story(id, details, recording, permissionRecordedAt);
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

        story.OriginalApproved = originalApproved;
        story.ReviewVersion = reviewVersion ?? Guid.NewGuid();
        if (story.ReviewVersion == Guid.Empty)
        {
            throw new DomainException("A review version is required.");
        }

        return story;
    }
}
