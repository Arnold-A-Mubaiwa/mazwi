namespace Mazwi.Application;

public interface IStorySubtitleService
{
    Task<StoryDraftDto?> SaveTimingAsync(Guid id, string language, IReadOnlyList<int> startsMs, int endMs, Guid reviewVersion, CancellationToken cancellationToken);
}
