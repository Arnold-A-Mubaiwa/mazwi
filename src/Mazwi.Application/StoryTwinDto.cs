using Mazwi.Domain;

namespace Mazwi.Application;

/// <summary>A twin as one artist sees it. Gists, vectors and similarity scores are never included.</summary>
public sealed record StoryTwinDto(Guid StoryId, string StoryTitle, Guid TwinStoryId, string TwinTitle, string TwinArtistName,
    string TwinLanguage, string TwinCommunity, string Status)
{
    public static StoryTwinDto From(TwinView view) => new(view.StoryId, view.StoryTitle, view.TwinStoryId, view.TwinTitle,
        view.TwinArtistName, view.TwinLanguage, view.TwinCommunity, view.Status.ToString().ToLowerInvariant());
}
