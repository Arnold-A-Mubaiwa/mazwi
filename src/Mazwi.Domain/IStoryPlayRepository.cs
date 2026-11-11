namespace Mazwi.Domain;

public interface IStoryPlayRepository
{
    Task RecordAsync(StoryPlay play, CancellationToken cancellationToken);
    // Totals for every story the artist owns, with plays on or after `recentSince` counted as recent.
    Task<IReadOnlyList<StoryPlayTotal>> ListForOwnerAsync(Guid ownerId, DateOnly recentSince, CancellationToken cancellationToken);
}
