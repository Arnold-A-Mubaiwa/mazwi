namespace Mazwi.Domain;

public sealed record TranslatedRecording
{
    public const string StandardVoice = "standard";
    public const string CloneVoice = "clone";

    public Guid Id { get; }
    public StoryRecording File { get; }

    /// <summary>Null for an artist-provided recording; otherwise the kind of AI voice that produced it.</summary>
    public string? Voice { get; }

    public TranslatedRecording(Guid id, StoryRecording file, string? voice = null)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("A translated recording needs an identity.");
        }

        ArgumentNullException.ThrowIfNull(file);
        if (voice is not (null or StandardVoice or CloneVoice))
        {
            throw new DomainException("Choose a standard or clone AI voice.");
        }

        Id = id;
        File = file;
        Voice = voice;
    }
}
