using System.Text;

namespace Mazwi.Domain;

/// <summary>Translation text prepared for speech: limited to about two minutes and split at sentence ends.</summary>
public sealed record SpeechScript
{
    public const int MaximumCharacters = 1_800;
    public const int MaximumPartCharacters = 600;
    public const int MaximumAudioBytes = 48_000 * 120;
    public const int WavHeaderBytes = 44;

    public IReadOnlyList<string> Parts { get; }

    public SpeechScript(string text)
    {
        string value = text?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            throw new AiDraftNotAllowedException("Save translation text before generating a voice.");
        }

        if (value.Length > MaximumCharacters)
        {
            throw new AiDraftNotAllowedException("AI voices support up to 1,800 characters (about two minutes). Upload your own recording for longer text.");
        }

        Parts = Split(value);
    }

    private static List<string> Split(string text)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        foreach (string sentence in Sentences(text))
        {
            if (current.Length > 0 && current.Length + sentence.Length > MaximumPartCharacters)
            {
                Add(parts, current);
            }

            foreach (string piece in Pieces(sentence))
            {
                if (current.Length > 0 && current.Length + piece.Length > MaximumPartCharacters)
                {
                    Add(parts, current);
                }

                current.Append(piece);
            }
        }

        Add(parts, current);
        return parts;
    }

    // Sentences keep their trailing whitespace and line breaks so poems keep their shape.
    private static IEnumerable<string> Sentences(string text)
    {
        int start = 0;
        for (int index = 0; index < text.Length; index++)
        {
            bool end = text[index] is '\n' || (text[index] is '.' or '!' or '?' or '…' && (index + 1 == text.Length || char.IsWhiteSpace(text[index + 1])));
            if (end)
            {
                int next = index + 1;
                while (next < text.Length && char.IsWhiteSpace(text[next]))
                {
                    next++;
                }

                yield return text[start..next];
                start = next;
                index = next - 1;
            }
        }

        if (start < text.Length)
        {
            yield return text[start..];
        }
    }

    // A sentence longer than one part is cut at word boundaries (or hard-cut if it has no spaces).
    private static IEnumerable<string> Pieces(string sentence)
    {
        int start = 0;
        while (sentence.Length - start > MaximumPartCharacters)
        {
            int cut = sentence.LastIndexOf(' ', start + MaximumPartCharacters - 1, MaximumPartCharacters);
            int end = cut > start ? cut + 1 : start + MaximumPartCharacters;
            yield return sentence[start..end];
            start = end;
        }

        yield return sentence[start..];
    }

    private static void Add(List<string> parts, StringBuilder current)
    {
        string part = current.ToString().Trim();
        if (part.Length > 0)
        {
            parts.Add(part);
        }

        current.Clear();
    }
}
