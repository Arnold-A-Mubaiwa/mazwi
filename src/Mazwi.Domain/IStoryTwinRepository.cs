namespace Mazwi.Domain;

public interface IStoryTwinRepository
{
    /// <summary>Twinnable stories whose fingerprint is missing or stale; failed ones only after <paramref name="retryFailedBefore"/>.</summary>
    Task<IReadOnlyList<StoryToFingerprint>> ListStoriesToFingerprintAsync(DateTimeOffset retryFailedBefore, int limit, CancellationToken cancellationToken);
    Task SaveFingerprintAsync(Guid storyId, string sourceHash, StoryFingerprint fingerprint, CancellationToken cancellationToken);
    Task SaveFingerprintFailureAsync(Guid storyId, string sourceHash, DateTimeOffset failedAt, CancellationToken cancellationToken);
    /// <summary>Every twinnable story with a current fingerprint.</summary>
    Task<IReadOnlyList<TwinCandidate>> ListCandidatesAsync(CancellationToken cancellationToken);
    /// <summary>Adds new pairs; existing pairs (including rejected ones) are left untouched.</summary>
    Task AddSuggestionsAsync(IEnumerable<StoryTwin> twins, CancellationToken cancellationToken);
    /// <summary>A pair that is not rejected and whose two stories are both still twinnable.</summary>
    Task<StoryTwin?> GetVisibleAsync(Guid story, Guid other, CancellationToken cancellationToken);
    Task SaveStatusAsync(StoryTwin twin, CancellationToken cancellationToken);
    Task<IReadOnlyList<TwinView>> ListForOwnerAsync(Guid ownerId, CancellationToken cancellationToken);
    /// <summary>Confirmed twins of a story that are still twinnable, for viewers.</summary>
    Task<IReadOnlyList<Guid>> ListConfirmedTwinIdsAsync(Guid storyId, CancellationToken cancellationToken);
}
