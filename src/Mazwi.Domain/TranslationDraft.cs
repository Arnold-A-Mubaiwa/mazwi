namespace Mazwi.Domain;

public sealed record TranslationDraft
{
    public string Language { get; }
    public StoryText Text { get; }
    public TranslationNotes Notes { get; }
    public TranslatedRecording? Recording { get; }
    public bool IsApproved { get; }
    public string Status => IsApproved ? "approved" : "draft";

    public TranslationDraft(string language, string text, TranslatedRecording? recording = null, bool isApproved = false, TranslationNotes? notes = null)
    {
        Language = language?.Trim() ?? string.Empty;
        if (!StoryDetails.SupportsLanguage(Language))
        {
            throw new DomainException("Choose Shona, isiXhosa, Swahili, or English for the translation.");
        }

        Text = new StoryText(text);
        Notes = notes ?? new TranslationNotes();
        Recording = recording;
        IsApproved = isApproved;
    }
}
