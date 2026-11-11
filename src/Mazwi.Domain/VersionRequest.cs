namespace Mazwi.Domain;

/// <summary>
/// An anonymous "please tell this story in my language". Only counts are kept: nothing about the listener.
/// </summary>
public sealed record VersionRequest
{
    public Guid StoryId { get; }
    public string Language { get; }

    private VersionRequest(Guid storyId, string language)
    {
        StoryId = storyId;
        Language = language;
    }

    public static VersionRequest For(Story story, string language)
    {
        ArgumentNullException.ThrowIfNull(story);
        story.EnsureVersionCanBeRequested(language);
        return new VersionRequest(story.Id, language);
    }
}
