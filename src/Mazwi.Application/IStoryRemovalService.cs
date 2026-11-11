namespace Mazwi.Application;

public interface IStoryRemovalService
{
    Task<StoryDraftDto?> UnpublishAsync(Guid id, Guid reviewVersion, CancellationToken cancellationToken);
    // Permanently deletes the story, its translations and every recording file. False when it does not exist.
    Task<bool> DeleteAsync(Guid id, Guid reviewVersion, CancellationToken cancellationToken);
}
