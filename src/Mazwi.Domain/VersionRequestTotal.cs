namespace Mazwi.Domain;

/// <summary>How many listeners asked for a story in a language it is not yet approved in.</summary>
public sealed record VersionRequestTotal
{
    public Guid StoryId { get; }
    public string Language { get; }
    public int Count { get; }

    public VersionRequestTotal(Guid storyId, string language, int count)
    {
        if (storyId == Guid.Empty || !StoryDetails.SupportsLanguage(language) || count < 1)
        {
            throw new DomainException("A request total needs a story, a supported language and a positive count.");
        }

        StoryId = storyId;
        Language = language;
        Count = count;
    }
}
