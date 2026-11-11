using Mazwi.Domain;

namespace Mazwi.Application;

public sealed class StoryTwinService(IStoryTwinRepository twins, IStoryRepository stories, IStoryFingerprinter fingerprinter,
    TwinThreshold threshold, TimeProvider clock) : IStoryTwinService
{
    /// <summary>A failed fingerprint is retried after this long, so an AI outage does not loop.</summary>
    public static readonly TimeSpan RetryAfterFailure = TimeSpan.FromMinutes(30);

    public async Task<int> RefreshAsync(int limit, CancellationToken cancellationToken)
    {
        IReadOnlyList<StoryToFingerprint> work = await twins.ListStoriesToFingerprintAsync(clock.GetUtcNow() - RetryAfterFailure, limit, cancellationToken);
        var refreshed = new List<Guid>();
        foreach (StoryToFingerprint story in work)
        {
            try
            {
                StoryFingerprint fingerprint = await fingerprinter.FingerprintAsync(story.Text, story.Language, story.StoryType, cancellationToken);
                await twins.SaveFingerprintAsync(story.StoryId, story.SourceHash, fingerprint, cancellationToken);
                refreshed.Add(story.StoryId);
            }
            catch (StoryFingerprintUnavailableException)
            {
                await twins.SaveFingerprintFailureAsync(story.StoryId, story.SourceHash, clock.GetUtcNow(), cancellationToken);
            }
        }

        if (refreshed.Count == 0)
        {
            return 0;
        }

        IReadOnlyList<TwinCandidate> candidates = await twins.ListCandidatesAsync(cancellationToken);
        foreach (TwinCandidate candidate in candidates.Where(candidate => refreshed.Contains(candidate.StoryId)))
        {
            await twins.AddSuggestionsAsync(TwinMatcher.Match(candidate, candidates, threshold), cancellationToken);
        }

        return refreshed.Count;
    }

    public async Task<IReadOnlyList<StoryTwinDto>> ListForOwnerAsync(Guid ownerId, CancellationToken cancellationToken) =>
        (await twins.ListForOwnerAsync(ownerId, cancellationToken)).Select(StoryTwinDto.From).ToArray();

    public Task<bool> ConfirmAsync(Guid storyId, Guid twinStoryId, CancellationToken cancellationToken) =>
        ChangeAsync(storyId, twinStoryId, twin => twin.Confirm(), cancellationToken);

    public Task<bool> RejectAsync(Guid storyId, Guid twinStoryId, CancellationToken cancellationToken) =>
        ChangeAsync(storyId, twinStoryId, twin => twin.Reject(), cancellationToken);

    public async Task<StoryDraftDto?> SetTwinsAllowedAsync(Guid storyId, bool allowed, CancellationToken cancellationToken)
    {
        Story? story = await stories.GetAsync(storyId, cancellationToken);
        if (story is null)
        {
            return null;
        }

        story.AllowTwins(allowed);
        await stories.SaveTwinsAllowedAsync(story, cancellationToken);
        return StoryDraftDto.From(story);
    }

    private async Task<bool> ChangeAsync(Guid storyId, Guid twinStoryId, Action<StoryTwin> change, CancellationToken cancellationToken)
    {
        StoryTwin? twin = await twins.GetVisibleAsync(storyId, twinStoryId, cancellationToken);
        if (twin is null)
        {
            return false;
        }

        change(twin);
        await twins.SaveStatusAsync(twin, cancellationToken);
        return true;
    }
}
