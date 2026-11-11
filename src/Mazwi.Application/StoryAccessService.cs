using Mazwi.Domain;

namespace Mazwi.Application;

public sealed class StoryAccessService(IStoryRepository repository) : IStoryAccessService
{
    public async Task<StoryAccess> CheckOwnerAsync(Guid storyId, Guid artistId, CancellationToken cancellationToken)
    {
        Story? story = await repository.GetAsync(storyId, cancellationToken);
        return story is null ? StoryAccess.NotFound : story.IsOwnedBy(artistId) ? StoryAccess.Allowed : StoryAccess.Denied;
    }
}
