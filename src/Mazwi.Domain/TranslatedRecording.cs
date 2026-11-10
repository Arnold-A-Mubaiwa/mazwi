namespace Mazwi.Domain;

public sealed record TranslatedRecording
{
    public Guid Id { get; }
    public StoryRecording File { get; }

    public TranslatedRecording(Guid id, StoryRecording file)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("A translated recording needs an identity.");
        }

        ArgumentNullException.ThrowIfNull(file);
        Id = id;
        File = file;
    }
}
