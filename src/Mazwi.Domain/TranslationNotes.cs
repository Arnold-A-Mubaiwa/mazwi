namespace Mazwi.Domain;

public sealed record TranslationNotes
{
    public const int MaximumCount = 20;
    public const int MaximumLength = 1000;
    public IReadOnlyList<string> Items { get; }

    public TranslationNotes(IEnumerable<string>? notes = null)
    {
        string[] values = notes?.ToArray() ?? [];
        if (values.Length > MaximumCount || values.Any(note => string.IsNullOrWhiteSpace(note)
            || note.Length > MaximumLength || note.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t'))))
        {
            throw new DomainException("Use up to 20 non-empty translation notes, each at most 1,000 characters.");
        }

        Items = Array.AsReadOnly(values.Select(note => note.Trim()).ToArray());
    }
}
