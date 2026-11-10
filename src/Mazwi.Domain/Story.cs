namespace Mazwi.Domain;

public sealed class Story
{
    private readonly Dictionary<string, TranslationDraft> _translations = new(StringComparer.Ordinal);
    public Guid Id { get; }
    public StoryDetails Details { get; }
    public StoryRecording Recording { get; }
    public DateTimeOffset PermissionRecordedAt { get; }
    public string Status => "unpublished";
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

    public void SetTranscript(string text) => Transcript = new StoryText(text);

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

        _translations[draft.Language] = draft;
        return draft;
    }

    public static Story Rehydrate(Guid id, StoryDetails details, StoryRecording recording, DateTimeOffset permissionRecordedAt,
        string? transcript = null, IEnumerable<TranslationDraft>? translations = null)
    {
        var story = new Story(id, details, recording, permissionRecordedAt);
        if (transcript is not null)
        {
            story.SetTranscript(transcript);
        }

        foreach (TranslationDraft draft in translations ?? [])
        {
            story.SetTranslation(draft.Language, draft.Text.Value);
        }

        return story;
    }
}
