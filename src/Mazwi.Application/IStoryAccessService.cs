namespace Mazwi.Application;

public enum StoryAccess
{
    NotFound,
    Denied,
    Allowed,
}

public interface IStoryAccessService
{
    Task<StoryAccess> CheckOwnerAsync(Guid storyId, Guid artistId, CancellationToken cancellationToken);
}
