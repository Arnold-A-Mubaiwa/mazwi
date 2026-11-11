namespace Mazwi.Application;

public interface IStoryStatsService
{
    // True when the play was counted; false when the story or version is not an approved, current one.
    Task<bool> RecordPlayAsync(Guid id, string language, Guid version, CancellationToken cancellationToken);
    Task<IReadOnlyList<StoryStatsDto>> GetForOwnerAsync(Guid ownerId, CancellationToken cancellationToken);
}
