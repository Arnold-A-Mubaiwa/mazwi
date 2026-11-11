namespace Mazwi.Domain;

public sealed record TranslationDraft
{
    public string Language { get; }
    public StoryText Text { get; }
    public TranslationNotes Notes { get; }
    public TranslatedRecording? Recording { get; }
    public bool IsApproved { get; }
    // True only when the artist said they speak this language and checked the text. Approval can be given without it.
    public bool SpeakerChecked { get; }
    public string Status => IsApproved ? "approved" : "draft";

    public TranslationDraft(string language, string text, TranslatedRecording? recording = null, bool isApproved = false, TranslationNotes? notes = null, bool speakerChecked = false)
    {
        Language = language?.Trim() ?? string.Empty;
        if (!StoryDetails.SupportsLanguage(Language))
        {
            throw new DomainException("Choose one of the languages Mazwi supports for the translation.");
        }

        Text = new StoryText(text);
        Notes = notes ?? new TranslationNotes();
        Recording = recording;
        if (speakerChecked && !isApproved)
        {
            throw new DomainException("Only an approved translation can be marked as checked by a speaker.");
        }

        IsApproved = isApproved;
        SpeakerChecked = speakerChecked;
    }
}
