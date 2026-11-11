namespace Mazwi.Application;

public interface IStoryTwinService
{
    /// <summary>Fingerprints up to <paramref name="limit"/> published stories that need it and suggests their twins.</summary>
    Task<int> RefreshAsync(int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<StoryTwinDto>> ListForOwnerAsync(Guid ownerId, CancellationToken cancellationToken);
    /// <summary>False when the pair does not exist, was rejected, or one story can no longer be twinned.</summary>
    Task<bool> ConfirmAsync(Guid storyId, Guid twinStoryId, CancellationToken cancellationToken);
    Task<bool> RejectAsync(Guid storyId, Guid twinStoryId, CancellationToken cancellationToken);
    /// <summary>Confirmed twins of a story in the feed; null when the story is not in the feed.</summary>
    Task<IReadOnlyList<ViewerTwinDto>?> ListForViewerAsync(Guid storyId, string language, CancellationToken cancellationToken);
    Task<StoryDraftDto?> SetTwinsAllowedAsync(Guid storyId, bool allowed, CancellationToken cancellationToken);
}
