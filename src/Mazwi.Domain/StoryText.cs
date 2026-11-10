namespace Mazwi.Domain;

public sealed record StoryText
{
    public const int MaximumLength = 20_000;
    public string Value { get; }

    public StoryText(string value)
    {
        string text = value?.Trim() ?? string.Empty;
        if (text.Length is 0 or > MaximumLength || text.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t')))
        {
            throw new DomainException("Enter 1–20,000 characters of story text. Line breaks and tabs are allowed.");
        }

        Value = text;
    }
}
