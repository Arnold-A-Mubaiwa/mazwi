namespace Mazwi.Domain;

/// <summary>Who may be twinned with whom: other artists, other languages, close enough.</summary>
public static class TwinMatcher
{
    public const int MaxSuggestionsPerStory = 5;

    public static IReadOnlyList<StoryTwin> Match(TwinCandidate story, IEnumerable<TwinCandidate> others, TwinThreshold threshold)
    {
        ArgumentNullException.ThrowIfNull(story);
        ArgumentNullException.ThrowIfNull(others);
        ArgumentNullException.ThrowIfNull(threshold);
        return others
            .Where(other => other.StoryId != story.StoryId && other.OwnerId != story.OwnerId && other.Language != story.Language)
            .Select(other => (other.StoryId, Similarity: story.Fingerprint.SimilarityTo(other.Fingerprint)))
            .Where(match => match.Similarity >= threshold.Value)
            .OrderByDescending(match => match.Similarity)
            .Take(MaxSuggestionsPerStory)
            .Select(match => StoryTwin.Suggest(story.StoryId, match.StoryId, match.Similarity))
            .ToArray();
    }
}

/// <summary>A twin as one artist sees it in Studio.</summary>
public sealed record TwinView(Guid StoryId, string StoryTitle, Guid TwinStoryId, string TwinTitle, string TwinArtistName,
    string TwinLanguage, string TwinCommunity, TwinStatus Status);
