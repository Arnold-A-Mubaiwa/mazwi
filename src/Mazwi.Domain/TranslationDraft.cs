namespace Mazwi.Domain;

public sealed record TranslationDraft
{
    public string Language { get; }
    public StoryText Text { get; }
    public string Status => "draft";

    public TranslationDraft(string language, string text)
    {
        Language = language?.Trim() ?? string.Empty;
        if (!StoryDetails.SupportsLanguage(Language))
        {
            throw new DomainException("Choose Shona, isiXhosa, Swahili, or English for the translation.");
        }

        Text = new StoryText(text);
    }
}
