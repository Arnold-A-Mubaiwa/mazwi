namespace Mazwi.Domain;

public sealed record StoryDetails
{
    public string Title { get; }
    public string ArtistName { get; }
    public string Community { get; }
    public string Country { get; }
    public string Type { get; }
    public string Language { get; }

    public StoryDetails(string title, string artistName, string community, string country, string type, string language)
    {
        Title = Required(title, "Title", 200);
        ArtistName = Required(artistName, "Artist name", 100);
        Community = Required(community, "Community", 100);
        Country = Required(country, "Country", 2).ToUpperInvariant();
        if (Country.Length != 2 || Country.Any(c => c is < 'A' or > 'Z'))
        {
            throw new DomainException("Country must be a two-letter country code.");
        }

        Type = Required(type, "Story type", 20);
        Language = Required(language, "Language", 2);
        if (Type is not ("story" or "poem" or "folktale" or "song"))
        {
            throw new DomainException("Choose story, poem, folktale, or song.");
        }

        if (!SupportsLanguage(Language))
        {
            throw new DomainException("Choose Shona, isiXhosa, Swahili, or English.");
        }
    }

    public static bool SupportsLanguage(string language) => language is "sn" or "xh" or "sw" or "en";

    private static string Required(string? value, string name, int maximum)
    {
        string text = value?.Trim() ?? string.Empty;
        if (text.Length is 0 || text.Length > maximum || text.Any(char.IsControl))
        {
            throw new DomainException($"{name} must contain 1–{maximum} characters without control characters.");
        }

        return text;
    }
}
