namespace Mazwi.Domain;

/// <summary>A language a story can be told, translated or watched in.</summary>
/// <param name="Code">ISO 639-1 code, two lowercase letters.</param>
/// <param name="Name">The name Mazwi shows in sentences ("Approved isiXhosa text").</param>
/// <param name="NativeName">The name speakers use for their own language.</param>
/// <param name="EnglishName">The English name, used in AI instructions.</param>
/// <param name="Greeting">A short hello in the language.</param>
/// <param name="AiChecked">
/// True only for the pilot languages Mazwi was built and tested with. For every other language, AI drafts and voices
/// have not been checked by Mazwi and artists are told so.
/// </param>
public sealed record StoryLanguage(string Code, string Name, string NativeName, string EnglishName, string Greeting, bool AiChecked);

/// <summary>The one catalogue of languages Mazwi supports. Every layer asks this catalogue; nothing else lists codes.</summary>
public static class StoryLanguages
{
    public const string Default = "sn";

    public static IReadOnlyList<StoryLanguage> All { get; } =
    [
        // Southern Africa
        new("sn", "Shona", "chiShona", "Shona", "Mhoro", true),
        new("nd", "isiNdebele", "isiNdebele", "Northern Ndebele (Zimbabwe)", "Salibonani", false),
        new("xh", "isiXhosa", "isiXhosa", "Xhosa", "Molo", true),
        new("zu", "isiZulu", "isiZulu", "Zulu", "Sawubona", false),
        new("st", "Sesotho", "Sesotho", "Southern Sotho", "Lumela", false),
        new("tn", "Setswana", "Setswana", "Tswana", "Dumela", false),
        new("af", "Afrikaans", "Afrikaans", "Afrikaans", "Hallo", false),
        new("ny", "Chichewa", "Chichewa", "Chichewa (Nyanja)", "Moni", false),
        new("mg", "Malagasy", "Malagasy", "Malagasy", "Manao ahoana", false),
        // East Africa and the Horn
        new("sw", "Kiswahili", "Kiswahili", "Swahili", "Habari", true),
        new("lg", "Luganda", "Luganda", "Ganda (Luganda)", "Oli otya", false),
        new("rw", "Kinyarwanda", "Ikinyarwanda", "Kinyarwanda", "Muraho", false),
        new("so", "Somali", "Soomaali", "Somali", "Salaan", false),
        new("am", "Amharic", "አማርኛ", "Amharic", "ሰላም", false),
        new("om", "Oromo", "Afaan Oromoo", "Oromo", "Akkam", false),
        // West and Central Africa
        new("yo", "Yoruba", "Yorùbá", "Yoruba", "Ẹ n lẹ", false),
        new("ig", "Igbo", "Igbo", "Igbo", "Ndewo", false),
        new("ha", "Hausa", "Hausa", "Hausa", "Sannu", false),
        new("ak", "Akan", "Akan", "Akan (Twi)", "Akwaaba", false),
        new("wo", "Wolof", "Wolof", "Wolof", "Na nga def", false),
        new("ln", "Lingala", "Lingála", "Lingala", "Mbote", false),
        // Shared across Africa
        new("ar", "Arabic", "العربية", "Arabic", "مرحبا", false),
        new("fr", "French", "Français", "French", "Bonjour", false),
        new("pt", "Portuguese", "Português", "Portuguese", "Olá", false),
        new("en", "English", "English", "English", "Hello", true),
    ];

    private static readonly Dictionary<string, StoryLanguage> ByCode = All.ToDictionary(language => language.Code, StringComparer.Ordinal);

    public static bool IsSupported(string? code) => code is not null && ByCode.ContainsKey(code);

    public static StoryLanguage? Find(string? code) => code is not null && ByCode.TryGetValue(code, out StoryLanguage? language) ? language : null;

    /// <summary>Returns the language or throws a <see cref="DomainException"/> for an unsupported code.</summary>
    public static StoryLanguage Get(string? code) =>
        Find(code) ?? throw new DomainException("Choose one of the languages Mazwi supports.");
}
